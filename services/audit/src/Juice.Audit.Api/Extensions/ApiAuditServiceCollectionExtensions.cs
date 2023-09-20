using Grpc.Net.ClientFactory;
using Juice.Audit.Api.NotificationHandlers;
using Juice.EF;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ApiAuditServiceCollectionExtensions

    {
        /// <summary>
        /// Handle DataEvent from <c>INotificationHandler<DataEvent></c> to fulfill audit information
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddDataEventHandler(this IServiceCollection services)
        {
            services.AddScoped<INotificationHandler<DataEvent>, DataEvenNotificationtHandler>();
            return services;
        }

        /// <summary>
        /// Use with <c>MapAuditGrpcServer()</c> to handle audit data from gRPC client
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <param name="configureDb"></param>
        /// <returns></returns>
        public static IServiceCollection ConfigureAuditGrpcHost(this IServiceCollection services,
            IConfiguration configuration,
            Action<DbOptions>? configureDb = default)
        {
            services.AddDefaultAuditService()
                .AddAuditCommandHandlers()
                .AddAuditDbContext(configuration, configureDb)
                .AddEFAuditRepos();
            return services;
        }

        /// <summary>
        /// Use with <c>UseAudit()</c> to enable audit for specific paths
        /// <para>Store audit information to gRPC Audit service</para></para>
        /// </summary>
        /// <param name="services"></param>
        /// <param name="grpcEndpoint"></param>
        /// <returns></returns>
        public static IServiceCollection ConfigureAuditGrpcClient(this IServiceCollection services, Action<GrpcClientFactoryOptions> configure)
        {
            services.AddGrpcAuditService(configure)
                .AddAuditContextAccessor()
                .AddDataEventHandler();
            return services;
        }

        /// <summary>
        /// Use with <c>UseAudit()</c> to enable audit for specific paths
        /// <para>Store audit information to EF repository directly</para>
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <param name="configureDb"></param>
        /// <returns></returns>
        public static IServiceCollection ConfigureAuditDefault(this IServiceCollection services,
                       IConfiguration configuration,
                       Action<DbOptions>? configureDb = default)
        {
            services.AddDefaultAuditService()
                .AddAuditCommandHandlers()
                .AddAuditDbContext(configuration, configureDb)
                .AddEFAuditRepos()
                .AddAuditContextAccessor()
                .AddDataEventHandler();
            return services;
        }
    }
}
