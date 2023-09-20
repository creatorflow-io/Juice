using Grpc.Net.ClientFactory;
using Juice;
using Juice.Audit;
using Juice.Audit.Api.Grpc.Services;
using Juice.Audit.CommandHandlers;
using Juice.Audit.Commands;
using Juice.Audit.Services;
using MediatR;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class AuditServiceCollectionExtensions
    {
        public static IServiceCollection AddAuditContextAccessor(this IServiceCollection services)
        {
            services.AddScoped<IAuditContextAccessor, DefaultAuditContextAccessor>();
            return services;
        }

        /// <summary>
        /// Persist audit information by send a SaveAuditInfoCommand to the IMediator (data will store to repositories directly)
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddDefaultAuditService(this IServiceCollection services)
        {
            services.AddScoped<IAuditService, DefaultAuditService>();
            return services;
        }

        /// <summary>
        /// Persist audit information by send an message to the gRPC Audit service (data will store to repositories remotely)
        /// </summary>
        /// <param name="services"></param>
        /// <param name="grpcEndpoint"></param>
        /// <returns></returns>
        public static IServiceCollection AddGrpcAuditService(this IServiceCollection services, Action<GrpcClientFactoryOptions> comfigure)
        {
            services.AddGrpcClient<Juice.Audit.Grpc.AuditGrpcService.AuditGrpcServiceClient>(comfigure);

            services.AddScoped<IAuditService, GrpcAuditService>();
            return services;
        }


        public static IServiceCollection AddAuditCommandHandlers(this IServiceCollection services)
        {
            services.AddScoped<IRequestHandler<SaveAuditInfoCommand, IOperationResult>, SaveAuditInfoCommandHandler>();
            return services;
        }
    }
}
