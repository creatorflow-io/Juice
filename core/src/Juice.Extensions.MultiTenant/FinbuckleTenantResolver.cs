
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Juice.MultiTenant;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.Extensions.MultiTenant
{
    internal class FinbuckleTenantResolver<TTenant> : IScopedTenantResolver<TTenant>
        where TTenant : class, ITenant, ITenantInfo, new()
    {
        private readonly IMultiTenantContextSetter _tenantContextSetter;
        private readonly IMultiTenantContextAccessor _tenantContextAccessor;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IServiceProvider _serviceProvider;

        public FinbuckleTenantResolver(
            IMultiTenantContextSetter tenantContextSetter,
            IMultiTenantContextAccessor tenantContextAccessor,
            IServiceProvider serviceProvider,
            ILoggerFactory logger)
        {
            _tenantContextAccessor = tenantContextAccessor;
            _tenantContextSetter = tenantContextSetter;
            _serviceProvider = serviceProvider;
            _loggerFactory = logger;
        }

        public IDisposable Resolve(string? tenantId)
        {
            if (tenantId is null)
            {
                return Resolve((TTenant?)null);
            }
            using var scope = _serviceProvider.CreateScope();
            var stores = scope.ServiceProvider.GetServices<IMultiTenantStore<TTenant>>();
            foreach (var store in stores)
            {
                var tenant = store.TryGetAsync(tenantId).GetAwaiter().GetResult();
                if (tenant is not null)
                {
                    return Resolve(tenant);
                }
            }
            return Resolve((TTenant?)null);
        }

        public IDisposable Resolve(TTenant? tenant)
        {
            var previousContext = _tenantContextAccessor.MultiTenantContext;

            _tenantContextSetter.MultiTenantContext = new MultiTenantContext<TTenant>
            {
                TenantInfo = tenant
            };
            var logger = _loggerFactory.CreateLogger<FinbuckleTenantResolver<TTenant>>();
            logger.LogInformation("Resolved Tenant: {tenantIdentifier} {tenantId}",
                _tenantContextAccessor.MultiTenantContext.TenantInfo?.Identifier,
                _tenantContextAccessor.MultiTenantContext.TenantInfo?.Id);
            return new TenantScope(() =>
            {
                logger.LogInformation("Disposed Tenant scope: {tenantId}",
                    _tenantContextAccessor.MultiTenantContext.TenantInfo?.Identifier);
                _tenantContextSetter.MultiTenantContext = previousContext;
                logger.LogInformation("Restored previous Tenant: {tenantId}",
                    _tenantContextAccessor.MultiTenantContext?.TenantInfo?.Identifier);
            });
        }

    }

    internal class TenantScope : IDisposable
    {
        private readonly Action _onDispose;
        public TenantScope(Action onDispose) => _onDispose = onDispose;
        public void Dispose() => _onDispose();
    }
}
