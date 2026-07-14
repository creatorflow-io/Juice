namespace Juice.AspNetCore.Idempotency
{
    /// <summary>
    /// Opt-in marker: apply to a controller, action, or minimal-API endpoint to require and enforce
    /// an <c>Idempotency-Key</c> header. Endpoints without this attribute are unaffected and incur no
    /// idempotency overhead (FR-001, read-only endpoints excluded).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class IdempotentAttribute : Attribute
    {
        /// <summary>
        /// Optional explicit scope. When omitted the scope defaults to the endpoint/route id, so keys
        /// cannot collide across endpoints. Set a shared value to make an intentional cross-endpoint scope.
        /// </summary>
        public string? Scope { get; set; }
    }
}
