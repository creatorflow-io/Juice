using Grpc.Core;
using Juice.Audit.Commands;
using Juice.Audit.Grpc;
using MediatR;

namespace Juice.Audit.Api.Grpc.Services
{
    internal class AuditGrpcServer : AuditGrpcService.AuditGrpcServiceBase
    {
        private readonly IMediator _mediator;
        public AuditGrpcServer(IMediator mediator)
        {
            _mediator = mediator;
        }
        public override async Task<AuditOperationResult> Log(AuditRequest request, ServerCallContext context)
        {
            try
            {
                var ral = request.AccessLog;
                var accessLog = new Domain.AccessLogAggregate.AccessLog(ral.Action, ral.User);

                accessLog.SetRequestInfo(new Domain.AccessLogAggregate.RequestInfo(
                    ral.ReqMethod, ral.ReqPath, ral.ReqData.ToStringUtf8(), ral.ReqQuery,
                    ral.ReqHeaders.ToStringUtf8(), ral.ReqScheme, ral.ReqRemoteIp, ral.ReqId, ral.ReqHost
                    ));

                if (ral.Restricted)
                {
                    accessLog.Restricted();
                }

                var metadata = ral.Metadata.ToStringUtf8();
                if (!string.IsNullOrEmpty(metadata))
                {
                    accessLog.SetMetadataJson(metadata);
                }

                accessLog.UpdateResponseInfo(res =>
                {
                    if (ral.ResStatus.HasValue && ral.ResElapsed.HasValue)
                    {
                        res.SetResponseInfo(ral.ResStatus.Value, ral.ResHeaders.ToStringUtf8(),
                            ral.ResElapsed.Value);
                    }
                    var resData = ral.ResData.ToStringUtf8();
                    var resErr = ral.ResErr.ToStringUtf8();
                    var resMsg = ral.ResMsg.ToStringUtf8();
                    if (!string.IsNullOrEmpty(resData))
                    {
                        res.TrySetData(resData);
                    }
                    if (!string.IsNullOrEmpty(resErr))
                    {
                        res.TrySetError(resErr);
                    }
                    if (!string.IsNullOrEmpty(resMsg))
                    {
                        res.TrySetMessage(resMsg);
                    }
                });

                accessLog.SetServerInfo(new Domain.AccessLogAggregate.ServerInfo(
                    ral.SrvMachine, ral.SrvOS, ral.SrvAppVer, ral.SrvApp
                ));

                var auditEntries = request.AuditEntries.Select(ae =>
                    new Domain.DataAuditAggregate.DataAudit(
                        ae.User, ae.Datetime.ToDateTimeOffset(),
                        ae.Action, ae.Database, ae.Schema, ae.Table,
                        ae.KeyValues.ToStringUtf8(), ae.DataChanges.ToStringUtf8(), ae.ReqId
                )).ToArray();

                var saveAuditCommand = new SaveAuditInfoCommand(accessLog, auditEntries);
                var rs = await _mediator.Send(saveAuditCommand);
                if (!rs.Succeeded)
                {
                    return new AuditOperationResult()
                    {
                        Succeeded = false,
                        Message = rs.Message
                    };
                }
                return new AuditOperationResult()
                {
                    Succeeded = true
                };
            }
            catch (Exception ex)
            {
                return new AuditOperationResult()
                {
                    Succeeded = false,
                    Message = ex.Message
                };
            }
        }
    }
}
