using Juice.EventBus.NATS.Infrastructure;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class JetStreamHostExtensions
    {

        /// <summary>
        /// Verify or create the declared JetStream streams
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static Task InitJetStreamInfrastructureAsync(this IHost app)
            => app.Services.InitJetStreamInfrastructureAsync();

        public static Task InitJetStreamInfrastructureAsync(this IServiceProvider sp)
        {
            var initializers = sp.GetServices<JetStreamInfrastructureInitializer>();
            var tasks = initializers.Select(i => i.InitializeAsync());
            return Task.WhenAll(tasks);
        }
    }
}
