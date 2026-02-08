using Juice.Messaging;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class MessagingServiceCollectionExtensions
    {
        public static MessagingBuilder AddMessaging(this IServiceCollection services, Action<MessagingBuilder>? buildAction = default)
        {
            var builder = new MessagingBuilder(services);
            buildAction?.Invoke(builder);

            builder.AddDefaultSerializer();
            return builder;
        }
    }
}
