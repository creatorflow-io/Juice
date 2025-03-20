
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.Stores;
using Finbuckle.MultiTenant.Events;
using Juice.MultiTenant;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Juice.Extensions.MultiTenant
{
    internal class FinbuckleTenantResolver<TTenant>: IScopedTenantResolver
        where TTenant : class, ITenant, ITenantInfo, new()
    {
        private readonly IMultiTenantContextSetter _tenantContextSetter;
        private readonly IMultiTenantContextAccessor _tenantContextAccessor;
        private readonly IEnumerable<IMultiTenantStore<TTenant>> _stores;
        private readonly ILoggerFactory _loggerFactory;

        public FinbuckleTenantResolver(IMultiTenantContextSetter tenantContextSetter,
            IMultiTenantContextAccessor tenantContextAccessor,
            IEnumerable<IMultiTenantStore<TTenant>> stores,
            ILoggerFactory logger)
        {
            _tenantContextAccessor = tenantContextAccessor;
            _tenantContextSetter = tenantContextSetter;
            _stores = stores;
            _loggerFactory = logger;
        }

        public IDisposable Resolve(string? tenantId)
        {
            var previousContext = _tenantContextAccessor.MultiTenantContext;
            
            _tenantContextSetter.MultiTenantContext = new MultiTenantContext<TenantInfo>
            {
                TenantInfo = new TenantInfo { Id = tenantId, Identifier = tenantId }
            };
            var logger = _loggerFactory.CreateLogger<FinbuckleTenantResolver<TTenant>>();
            logger.LogInformation($"Resolved Tenant: {tenantId}");
            return new TenantScope(() =>
            {
                logger.LogInformation($"Disposed Tenant: {tenantId}");
                _tenantContextSetter.MultiTenantContext = previousContext;
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
