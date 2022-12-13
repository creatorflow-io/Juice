﻿using Juice.Extensions.Options.Stores;
using Juice.MultiTenant.Extensions.Options.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Juice.MultiTenant.Extensions.Options.DependencyInjection
{
    public static class TenantsOptionsStoreServiceCollectionExtensions
    {
        /// <summary>
        /// Save tenant settings to TenantSettings
        /// </summary>
        /// <param name="services"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        public static IServiceCollection AddTenantSettingsOptionsMutableStore(this IServiceCollection services)
        {
            services.TryAddScoped<ITenantsOptionsMutableStore, TenantSettingsOptionsMutableStore>();
            return services;
        }

    }
}