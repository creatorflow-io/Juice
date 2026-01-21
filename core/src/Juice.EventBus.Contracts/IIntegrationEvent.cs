namespace Juice.EventBus
{
	public interface IIntegrationEvent
	{
        Guid Id { get; }
        DateTime CreationDate { get; }
        string GetEventKey();
    }
}
