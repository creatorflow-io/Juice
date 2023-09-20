using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Juice.Audit.Domain.AccessLogAggregate;
using Juice.Audit.Domain.DataAuditAggregate;
using Juice.Audit.Grpc;
using Newtonsoft.Json;

namespace Juice.Audit.Api.Grpc.Services
{
    /// <summary>
    /// Implement IAuditService to persist audit information to Grpc server.
    /// </summary>
    internal class GrpcAuditService : IAuditService
    {
        private readonly AuditGrpcService.AuditGrpcServiceClient _client;
        public GrpcAuditService(AuditGrpcService.AuditGrpcServiceClient client)
        {
            _client = client;
        }
        public async Task<IOperationResult> PersistAuditInformationAsync(AccessLog accessLog, DataAudit[] auditEntries, CancellationToken token)
        {
            using var reqHeaders = GenerateStreamFromString(accessLog.Request?.Headers);
            using var reqData = GenerateStreamFromString(accessLog.Request?.Data);

            using var resHeaders = GenerateStreamFromString(accessLog.Response?.Headers);
            using var resData = GenerateStreamFromString(accessLog.Response?.Data);
            using var resErr = GenerateStreamFromString(accessLog.Response?.Err);
            using var resMsg = GenerateStreamFromString(accessLog.Response?.Msg);

            using var metadata = new MemoryStream();
            using var metadataWriter = new StreamWriter(metadata);
            using var metadataJsonWriter = new JsonTextWriter(metadataWriter);
            await accessLog.Metadata.WriteToAsync(metadataJsonWriter, token);

            var request = new AuditRequest
            {
                AccessLog = new AccessLogMessage
                {
                    User = accessLog.User,
                    Action = accessLog.Action,
                    Datetime = accessLog.DateTime.ToTimestamp(),
                    Metadata = ByteString.FromStream(metadata),
                    ReqId = accessLog.Request?.TraceId,
                    ReqMethod = accessLog.Request?.Method,
                    ReqPath = accessLog.Request?.Path,
                    ReqData = ByteString.FromStream(reqData),
                    ReqQuery = accessLog.Request?.Query,
                    ReqHeaders = ByteString.FromStream(reqHeaders),
                    ReqScheme = accessLog.Request?.Scheme,
                    ReqRemoteIp = accessLog.Request?.RIPA,
                    ReqHost = accessLog.Request?.Host,
                    ReqZone = accessLog.Request?.Zone,
                    ResStatus = accessLog.Response?.Status,
                    ResData = ByteString.FromStream(resData),
                    ResErr = ByteString.FromStream(resErr),
                    ResMsg = ByteString.FromStream(resMsg),
                    ResElapsed = accessLog.Response?.ElapsedMs,
                    ResHeaders = ByteString.FromStream(resHeaders),
                    SrvMachine = accessLog.Server?.Machine,
                    SrvOS = accessLog.Server?.OS,
                    SrvApp = accessLog.Server?.App,
                    SrvAppVer = accessLog.Server?.AppVer
                }
            };

            foreach (var ae in auditEntries)
            {
                using var kvps = GenerateStreamFromString(ae.Kvps);
                using var changes = GenerateStreamFromString(ae.Changes);
                request.AuditEntries.Add(new DataAuditMessage
                {
                    User = ae.User,
                    Datetime = ae.DateTime.ToTimestamp(),
                    Action = ae.Action,
                    Database = ae.Db,
                    Schema = ae.Schema,
                    Table = ae.Tbl,
                    KeyValues = ByteString.FromStream(kvps),
                    DataChanges = ByteString.FromStream(changes),
                    ReqId = ae.TraceId
                });
            }

            var result = await _client.LogAsync(request);

            if (!result.Succeeded)
            {
                return OperationResult.Failed(result.Message);
            }
            return OperationResult.Success;
        }

        private static Stream GenerateStreamFromString(string? s)
            => StringUtils.GenerateStreamFromString(s);
    }
}
