namespace Juice.Messaging.Policies
{
    public sealed record PublishRoute(
        string PublisherKey,
        string Destination);
}
