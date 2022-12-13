using Finbuckle.MultiTenant;
using Juice.Domain;
using Juice.EF;
using Juice.EventBus.IntegrationEventLog.EF.DependencyInjection;
using Juice.Integrations.EventBus.DependencyInjection;
using Juice.Integrations.MediatR;
using Juice.Integrations.MediatR.DependencyInjection;
using Juice.MediatR.RequestManager.EF.DependencyInjection;
using Juice.MultiTenant.Api.Behaviors.DependencyInjection;
using Juice.MultiTenant.Api.Commands;
using Juice.MultiTenant.EF;
using Juice.MultiTenant.EF.DependencyInjection;
using Juice.MultiTenant.Finbuckle.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.MultiTenant.Api.DependencyInjection
{
    public static class FinbuckleMultiTenantBuilderExtensions
    {
        /// <summary>
        /// Configure Tenant microservice to provide tenant service, tenant settings service
        /// <para></para>JuiceIntegration
        /// <para></para>WithHeaderStrategy for grpc services
        /// <para></para>WithEFStore for Tenant EF store
        /// <para></para>TenantSettings
        /// <para></para>Configure MediatR, add Integration event service (NOTE: Required an event bus)
        /// </summary>
        /// <returns></returns>
        public static FinbuckleMultiTenantBuilder<TTenantInfo> ConfigureTenantHost<TTenantInfo>(this FinbuckleMultiTenantBuilder<TTenantInfo> builder, IConfiguration configuration,
            Action<DbOptions> configureTenantDb)
            where TTenantInfo : class, IDynamic, ITenantInfo, new()
        {
            builder.JuiceIntegration()
                    .WithHeaderStrategy()
                    .WithEFStore(configuration, configureTenantDb, true);

            builder.Services.AddMediatR(typeof(CreateTenantCommand).Assembly, typeof(AssemblySelector).Assembly);

            builder.Services
                .AddOperationExceptionBehavior()
                .AddMediatRTenantBehaviors()
                .AddMediatRTenantSettingsBehaviors()
                ;

            var dbOptions = new DbOptions<TenantStoreDbContext<TTenantInfo>>();
            configureTenantDb(dbOptions);

            builder.Services.AddIntegrationEventService()
                    .AddIntegrationEventLog()
                    .RegisterContext<TenantStoreDbContext<TTenantInfo>>(dbOptions.Schema)
                    .RegisterContext<TenantSettingsDbContext>(dbOptions.Schema);

            builder.Services.AddTenantSettingsDbContext(configuration, configureTenantDb);

            builder.Services.AddRequestManager(configuration, configureTenantDb);


            return builder;
        }

    }
}
