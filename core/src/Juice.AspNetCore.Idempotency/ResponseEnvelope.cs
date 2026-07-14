namespace Juice.AspNetCore.Idempotency
{
    /// <summary>
    /// Captured HTTP outcome stored against an idempotency key and replayed for matching retries (FR-003).
    /// </summary>
    public sealed class ResponseEnvelope
    {
        /// <summary>Original HTTP status code.</summary>
        public int StatusCode { get; set; }

        /// <summary>Original response content type (defaults to JSON).</summary>
        public string? ContentType { get; set; }

        /// <summary>Serialized response body.</summary>
        public string? Body { get; set; }
    }
}
