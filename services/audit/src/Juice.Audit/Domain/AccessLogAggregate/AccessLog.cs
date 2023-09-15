using Juice.Domain;
using MediatR;
using Newtonsoft.Json;

namespace Juice.Audit.Domain.AccessLogAggregate
{
    public class AccessLog : AggregrateRoot<INotification>
    {
        public AccessLog() { }
        public AccessLog(string action, string? user)
        {
            Action = action;
            User = user;
            DateTime = DateTimeOffset.UtcNow;
            ResponseInfo = new ResponseInfo();
        }
        public Guid Id { get; set; }
        public DateTimeOffset DateTime { get; init; }

        public string? User { get; init; }

        public string Action { get; private set; }

        public string ExtraMetadata { get; private set; } = "{}";

        public RequestInfo? RequestInfo { get; private set; }
        public ServerInfo? ServerInfo { get; private set; }
        public ResponseInfo? ResponseInfo { get; private set; }

        public void SetRequestInfo(RequestInfo requestInfo)
            => RequestInfo = requestInfo;

        public void SetServerInfo(ServerInfo serverInfo)
            => ServerInfo = serverInfo;

        public void UpdateResponseInfo(Action<ResponseInfo> update)
        {
            if (ResponseInfo is null)
            {
                ResponseInfo = new ResponseInfo();
            }
            update.Invoke(ResponseInfo);
        }

        public void SetExtraMetadata(string key, string value)
        {
            var metadata = JsonConvert.DeserializeObject<Dictionary<string, string>>(ExtraMetadata)
                ?? new Dictionary<string, string>();
            metadata[key] = value;
            ExtraMetadata = JsonConvert.SerializeObject(metadata);
        }

        public void SetAction(string action)
            => Action = action;
    }
}
