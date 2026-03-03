namespace Juice.EventBus.Publishing
{
    public sealed record PublishContext(
        string MessageId,
        string? Destination = null,
        string? TenantId = null,
        IDictionary<string, object?>? Headers = default,
        string? RoutingKey = null)
    {
        public PublishContext WithHeader(string key, object? value)
        {
            var headers = Headers ?? new Dictionary<string, object?>();
            headers[key] = value;
            return this with { Headers = headers };
        }

        public PublishContext WithHeaders(IDictionary<string, object?> headers)
        {
            var newHeaders = Headers ?? new Dictionary<string, object?>();
            foreach (var kvp in headers)
            {
                newHeaders[kvp.Key] = kvp.Value;
            }
            return this with { Headers = newHeaders };
        }
    }
}
