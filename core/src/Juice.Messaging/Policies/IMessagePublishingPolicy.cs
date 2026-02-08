namespace Juice.Messaging.Policies
{
    public interface IMessagePublishingPolicy
    {
        ValueTask<IReadOnlyCollection<PublishRoute>> ResolveAsync(PolicyResolveContext context);
    }
}
