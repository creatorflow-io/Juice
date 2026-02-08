namespace Juice
{
    public interface IMessage
    {
        Guid MessageId { get; }
        DateTimeOffset CreatedAt { get; }
        /// <summary>
        /// The tenant identifier for multi-tenant scenarios.
        /// </summary>
        string? TenantId { get; }
    }
}
