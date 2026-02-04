namespace Juice.EventBus
{
	public interface IIntegrationEvent
	{
        Guid Id { get; }
        DateTime CreationDate { get; }
        /// <summary>
        /// The tenant identifier for multi-tenant scenarios.
        /// </summary>
        string? TenantId { get; }
        /// <summary>
        /// The source domain where the event is generated. Useful to resolve publishing policies.
        /// </summary>
        string? Domain { get; }
        string GetEventKey();
    }
}
