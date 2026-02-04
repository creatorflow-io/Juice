namespace Juice.EventBus.Publishing.Policies
{
    public sealed record EventPublishRoute(
        string PublisherKey,
        string Destination);
}
