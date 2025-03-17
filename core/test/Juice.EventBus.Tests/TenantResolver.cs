using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant;
using Microsoft.Extensions.Logging;
using System;

namespace Juice.EventBus.Tests
{
    internal class TenantResolver : MultiTenant.IScopedTenantResolver
    {
        private readonly IMultiTenantContextSetter _tenantContextSetter;
        private readonly IMultiTenantContextAccessor _tenantContextAccessor;
        private readonly ILogger<TenantResolver> _logger;

        public TenantResolver(IMultiTenantContextSetter tenantContextSetter,
            IMultiTenantContextAccessor tenantContextAccessor,
            ILogger<TenantResolver> logger)
        {
            _tenantContextAccessor = tenantContextAccessor;
            _tenantContextSetter = tenantContextSetter;
            _logger = logger;
        }

        public IDisposable Resolve(string? tenantId)
        {
            var previousContext = _tenantContextAccessor.MultiTenantContext;
            _tenantContextSetter.MultiTenantContext = new MultiTenantContext<TenantInfo>
            {
                TenantInfo = new TenantInfo { Id = tenantId, Identifier = tenantId }
            };

            _logger.LogInformation($"Resolved Tenant: {tenantId}");
            return new TenantScope(() =>
            {
                _logger.LogInformation($"Disposed Tenant: {tenantId}");
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
