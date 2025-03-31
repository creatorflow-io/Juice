
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Juice.MultiTenant;
using Microsoft.Extensions.Logging;

namespace Juice.Extensions.MultiTenant
{
    internal class FinbuckleTenantResolver<TTenant> : IScopedTenantResolver<TTenant>
        where TTenant : class, ITenant, ITenantInfo, new()
    {
        private readonly IMultiTenantContextSetter _tenantContextSetter;
        private readonly IMultiTenantContextAccessor _tenantContextAccessor;
        private readonly ILoggerFactory _loggerFactory;

        public FinbuckleTenantResolver(IMultiTenantContextSetter tenantContextSetter,
            IMultiTenantContextAccessor tenantContextAccessor,
            ILoggerFactory logger)
        {
            _tenantContextAccessor = tenantContextAccessor;
            _tenantContextSetter = tenantContextSetter;
            _loggerFactory = logger;
        }

        public IDisposable Resolve(string? tenantId)
        {
            var tenantInfo = new TTenant();
            ((ITenantInfo)tenantInfo).Id = tenantId;
            ((ITenantInfo)tenantInfo).Identifier = tenantId;
            return Resolve(tenantInfo);
        }

        public IDisposable Resolve(TTenant? tenant)
        {
            var previousContext = _tenantContextAccessor.MultiTenantContext;

            _tenantContextSetter.MultiTenantContext = new MultiTenantContext<TTenant>
            {
                TenantInfo = tenant
            };
            var logger = _loggerFactory.CreateLogger<FinbuckleTenantResolver<TTenant>>();
            logger.LogInformation("Resolved Tenant: {tenantId}",((ITenant?) tenant)?.Identifier);
            return new TenantScope(() =>
            {
                logger.LogInformation("Disposed Tenant scope: {tenantId}", ((ITenant?)tenant)?.Identifier);
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
