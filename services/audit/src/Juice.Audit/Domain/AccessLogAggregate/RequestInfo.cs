using Juice.Domain;

namespace Juice.Audit.Domain.AccessLogAggregate
{
    public class RequestInfo : ValueObject
    {
        public RequestInfo() { }
        public RequestInfo(string method, string path, string? data, string? queryString, string? headers, string? scheme, string? remoteIpAddress, string requestId, string? host)
        {
            Method = method;
            Path = path;
            Data = data;
            Query = ValidatableExtensions.TrimExceededLength(queryString, LengthConstants.ShortDescriptionLength);
            Headers = ValidatableExtensions.TrimExceededLength(headers, LengthConstants.ShortDescriptionLength);
            Scheme = scheme;
            RIPA = remoteIpAddress;
            TraceId = requestId;
            Host = host;

            this.NotExceededLength(Method, LengthConstants.IdentityLength);
            this.NotExceededLength(TraceId, LengthConstants.IdentityLength);
            this.NotExceededLength(Host, LengthConstants.NameLength);
            this.NotExceededLength(Path, LengthConstants.NameLength);
            this.NotExceededLength(Scheme, LengthConstants.IdentityLength);
            this.NotExceededLength(RIPA, LengthConstants.IdentityLength);

            this.ThrowIfHasErrors();
        }
        public string Method { get; private set; }
        public string TraceId { get; private set; }
        public string? Host { get; private set; }
        public string Path { get; private set; }
        public string? Data { get; private set; }
        public string? Query { get; private set; }
        public string? Headers { get; private set; }
        public string? Scheme { get; private set; }
        public string? RIPA { get; private set; }
        public string? Zone { get; private set; }

        public void SetAccessZone(string accessZone)
        {
            Zone = accessZone;
            this.NotExceededLength(Zone, LengthConstants.NameLength);
            this.ThrowIfHasErrors();
        }

        protected override IEnumerable<object> GetEqualityComponents() =>
            new object[] { TraceId };
    }
}
