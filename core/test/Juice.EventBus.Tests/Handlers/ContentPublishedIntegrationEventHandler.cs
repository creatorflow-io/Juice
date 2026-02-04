using System.Threading.Tasks;
using Finbuckle.MultiTenant.Abstractions;
using Juice.EF.Tests.Events;
using Microsoft.Extensions.Logging;

namespace Juice.EventBus.Tests.Handlers
{
    public class ContentPublishedIntegrationEventHandler : IIntegrationEventHandler<ContentPublishedIntegrationEvent>
    {
        private ILogger _logger;
        private readonly HandledService _handledService;
        private readonly ScopedService? _scopedService;
        private readonly ITenantInfo? _tenantInfo;
        public ContentPublishedIntegrationEventHandler(ILogger<ContentPublishedIntegrationEventHandler> logger,
            HandledService handledService,
            IMultiTenantContextAccessor? tenantContextAccessor = default,
            ScopedService? scopedService = default)
        {
            _logger = logger;
            _handledService = handledService;
            _tenantInfo = tenantContextAccessor?.MultiTenantContext.TenantInfo;
            _scopedService = scopedService;
        }
        public async Task HandleAsync(ContentPublishedIntegrationEvent @event)
        {
            await Task.Delay(200);
            _logger.LogInformation("[X] Received {0} at {1}. TenantInfo: {2}", @event.Message, @event.CreationDate, _tenantInfo?.Identifier);
     
            if (_scopedService == null || !_scopedService.IsDisposed)
            {
                _handledService.Handle(nameof(ContentPublishedIntegrationEventHandler), @event.Id, _tenantInfo?.Identifier);
                _logger.LogInformation("Handled by {Handler}", nameof(ContentPublishedIntegrationEventHandler));
            }
        }
    }
}
