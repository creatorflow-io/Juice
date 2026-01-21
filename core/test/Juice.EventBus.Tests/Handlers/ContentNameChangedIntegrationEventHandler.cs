using System;
using System.Threading.Tasks;
using Juice.EF.Tests.Events;
using Microsoft.Extensions.Logging;
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Juice.Integrations.Tests")]
namespace Juice.EventBus.Tests.Handlers
{
    internal class ContentNameChangedIntegrationEventHandler : IIntegrationEventHandler<ContentNameChangedIntegrationEvent>
    {
        private readonly ILogger _logger;
        public ContentNameChangedIntegrationEventHandler(ILogger<ContentNameChangedIntegrationEventHandler> logger)
        {
            _logger = logger;
        }
        public Task HandleAsync(ContentNameChangedIntegrationEvent @event)
        {
            _logger.LogInformation("Content name changed event handled: ContentId={ContentId}, OldName={OldName}, NewName={NewName}",
                @event.ContentId, @event.OriginalName, @event.Name);
            return Task.CompletedTask;
        }
    }
}
