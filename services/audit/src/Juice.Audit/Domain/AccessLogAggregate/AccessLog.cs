using System.ComponentModel.DataAnnotations.Schema;
using Juice.Domain;
using MediatR;
using Newtonsoft.Json.Linq;

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
            Response = new ResponseInfo();
        }
        public Guid Id { get; set; }
        public DateTimeOffset DateTime { get; init; }

        public string? User { get; init; }

        public string Action { get; private set; }

        [NotMapped]
        public string? TraceId => Request?.TraceId;

        public JObject Metadata { get; private set; } = new JObject();

        public RequestInfo? Request { get; private set; }
        public ServerInfo? Server { get; private set; }
        public ResponseInfo? Response { get; private set; }

        public void SetRequestInfo(RequestInfo requestInfo)
        {
            Request = requestInfo;
        }

        public void SetServerInfo(ServerInfo serverInfo)
            => Server = serverInfo;

        public void UpdateResponseInfo(Action<ResponseInfo> update)
        {
            if (Response is null)
            {
                Response = new ResponseInfo();
            }
            update.Invoke(Response);
        }

        public void SetMetadata(string key, string value)
        {
            Metadata[key] = value;
        }

        public void SetMetadataJson(string json)
        {
            Metadata = JObject.Parse(json);
        }

        public void SetAction(string action)
            => Action = action;
    }
}
