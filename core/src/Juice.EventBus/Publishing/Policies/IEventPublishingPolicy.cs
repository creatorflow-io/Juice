namespace Juice.EventBus.Publishing.Policies
{
    public interface IEventPublishingPolicy
    {
        ValueTask<IReadOnlyCollection<EventPublishRoute>> ResolveAsync(PolicyResolveContext context);
    }
}
