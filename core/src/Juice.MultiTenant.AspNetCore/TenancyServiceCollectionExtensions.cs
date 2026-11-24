using Finbuckle.MultiTenant.Abstractions;
using Juice.MultiTenant;
using Juice.MultiTenant.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>Sets up the tenant pattern: <c>services.AddJuiceTenancy(...).WithStore&lt;TenantInfo&gt;()</c>, then <c>app.UseJuiceTenancy()</c>.</summary>
    public static class TenancyServiceCollectionExtensions
    {
        /// <summary>
        /// The tenant pattern's services: options (from the <c>Tenancy</c> section when <paramref name="configuration"/> is given),
        /// <see cref="ITenantContext"/>, <see cref="ITenantAccess"/> (<see cref="ClaimTenantAccess"/> unless one is registered), the problem
        /// writer and the <see cref="TenancyPolicies"/>. Then name the directory: <see cref="TenancyBuilder.WithStore{TTenant}"/>,
        /// <c>WithTenants</c> or <see cref="TenancyBuilder.WithDirectory{TDirectory}"/>; <c>UseJuiceTenancy()</c> refuses to start without one.
        /// </summary>
        public static TenancyBuilder AddJuiceTenancy(this IServiceCollection services, IConfiguration? configuration = null,
            Action<TenancyOptions>? configure = null)
        {
            var options = services.AddOptions<TenancyOptions>();
            if (configuration is not null)
            {
                options.Bind(configuration.GetSection("Tenancy"));
            }
            if (configure is not null)
            {
                options.Configure(configure);
            }
            services.AddHttpContextAccessor();
            services.TryAddScoped<TenantContext>();
            services.TryAddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
            services.TryAddSingleton<ITenantAccess, ClaimTenantAccess>();
            services.TryAddSingleton<ITenantRejectionWriter, ProblemTenantRejectionWriter>();
            services.TryAddSingleton<ITenantDirectoryCache, NoTenantDirectoryCache>();
            services.AddSingleton<IAuthorizationHandler, TenantAuthorizationHandler>();
            services.AddAuthorization(o =>
            {
                foreach (var requirement in Enum.GetValues<TenantRequirement>())
                {
                    o.AddPolicy(TenancyPolicies.For(requirement), p => p.AddRequirements(new TenantAuthorizationRequirement(requirement)));
                }
            });
            return new TenancyBuilder(services);
        }

        /// <summary>
        /// Resolves and checks the request's tenant: after <c>UseRouting()</c> (the route names the tenant) and <c>UseAuthentication()</c>,
        /// before <c>UseAuthorization()</c>. Don't also run Finbuckle's <c>UseMultiTenant()</c> after it: it would replace the checked tenant.
        /// </summary>
        public static IApplicationBuilder UseJuiceTenancy(this IApplicationBuilder app)
        {
            if (app.ApplicationServices.GetService<ITenantDirectory>() is null)
            {
                throw new InvalidOperationException(
                    "No tenant directory: call WithStore<TTenant>(), WithTenants(...) or WithDirectory<T>() after AddJuiceTenancy().");
            }
            return app.UseMiddleware<TenantResolutionMiddleware>();
        }
    }

    public sealed class TenancyBuilder(IServiceCollection services)
    {
        public IServiceCollection Services { get; } = services;

        /// <summary>Tenants from the Finbuckle stores registered for <typeparamref name="TTenant"/> (juice-tenants' gRPC or EF store).</summary>
        public TenancyBuilder WithStore<TTenant>() where TTenant : class, ITenantInfo, new()
        {
            Replace();
            Services.AddSingleton<StoreTenantDirectory<TTenant>>();
            Services.AddSingleton<ITenantDirectory>(sp => sp.GetRequiredService<StoreTenantDirectory<TTenant>>());
            Services.AddSingleton<ITenantDirectoryCache>(sp => sp.GetRequiredService<StoreTenantDirectory<TTenant>>());
            return this;
        }

        /// <summary>A fixed list of tenants (tests, local stacks, deployments without juice-tenants).</summary>
        public TenancyBuilder WithTenants(params TenantRecord[] tenants)
        {
            Replace();
            Services.AddSingleton<ITenantDirectory>(new FixedTenantDirectory(tenants));
            Services.AddSingleton<ITenantDirectoryCache, NoTenantDirectoryCache>();
            return this;
        }

        /// <summary>
        /// The tenants listed in configuration: <c>Tenancy:Tenants:0:Id</c>, <c>…:Identifier</c>, <c>…:Name</c> and <c>…:Status</c> (none is
        /// active). For local stacks.
        /// </summary>
        public TenancyBuilder WithTenants(IConfiguration configuration)
        {
            var tenants = configuration.GetSection("Tenancy:Tenants").GetChildren()
                .Select(t => TenantIds.TryParse(t["Id"], out var id)
                    ? new TenantRecord(id, t["Identifier"], t["Name"], TenantStatuses.Availability(t["Status"]))
                    : throw new InvalidOperationException($"Tenancy:Tenants:{t.Key}:Id isn't a tenant Id."))
                .ToArray();
            return WithTenants(tenants);
        }

        /// <summary>A directory of the service's own (register its <see cref="ITenantDirectoryCache"/> too if it remembers tenants).</summary>
        public TenancyBuilder WithDirectory<TDirectory>() where TDirectory : class, ITenantDirectory
        {
            Replace();
            Services.AddSingleton<ITenantDirectory, TDirectory>();
            Services.AddSingleton<ITenantDirectoryCache, NoTenantDirectoryCache>();
            return this;
        }

        /// <summary>A single-tenant deployment: every request is in <paramref name="tenantId"/>.</summary>
        public TenancyBuilder WithFixedTenant(Guid tenantId)
        {
            Services.Configure<TenancyOptions>(o => o.FixedTenantId = tenantId);
            return this;
        }

        private void Replace()
        {
            Services.RemoveAll<ITenantDirectory>();
            Services.RemoveAll<ITenantDirectoryCache>();
        }
    }
}
