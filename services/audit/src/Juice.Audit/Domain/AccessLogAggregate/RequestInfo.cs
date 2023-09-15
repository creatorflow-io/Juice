namespace Juice.Audit.Domain.AccessLogAggregate
{
    public class RequestInfo
    {
        public string Method { get; init; }
        public string? RequestId { get; init; }
        public string? Host { get; init; }
        public string Path { get; init; }
        public string? Data { get; init; }
        public string? QueryString { get; init; }
        public string? Headers { get; init; }
        public string? Schema { get; init; }
        public string? RemoteIpAddress { get; init; }
        public string? AccessZone { get; private set; }

        public void SetAccessZone(string accessZone)
        {
            AccessZone = accessZone;
        }

    }
}
