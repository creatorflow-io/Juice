using Juice.MediatR.Extensions;
using Juice.MultiTenant.Api.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.MultiTenant.Api.DependencyInjection
{
    public static class TenantCommandsServiceCollectionExtensions
    {
        public static IServiceCollection AddTenantCommands(this IServiceCollection services)
        {
            services.RegHandler<CreateTenantCommand, IOperationResult, CreateTenantCommandHandler>();
            services.RegIdentifiedHandler<CreateTenantCommand, IOperationResult, CreateTenantIdentifiedCommandHandler>();

            services.RegHandlers<DisableTenantCommand, IOperationResult,
                DisableTenantCommandHandler, DisableTenantIdentifiedCommandHandler>();

            services.RegHandlers<EnableTenantCommand, IOperationResult,
                EnableTenantCommandHandler, EnableTenantIdentifiedCommandHandler>();

            services.RegHandlers<DeleteTenantCommand, IOperationResult,
                DeleteTenantCommandHandler, DeleteTenantIdentifiedCommandHandler>();

            services.RegHandlers<UpdateTenantCommand, IOperationResult,
                UpdateTenantCommandHandler, UpdateTenantIdentifiedCommandHandler>();

            return services;
        }
    }
}
