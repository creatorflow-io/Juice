using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.EventBus
{
    /// <summary>
    /// Local event bus throught memory
    /// </summary>
    public class InMemoryEventBus : EventBusBase, IDisposable
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private List<Task> _tasks = new List<Task>();
        private readonly Lock _lock = new();
        private bool _disposedValue;

        public InMemoryEventBus(IEventBusSubscriptionsManager subscriptionsManager,
            IServiceScopeFactory scopeFactory,
            ILogger<InMemoryEventBus> logger) : base(subscriptionsManager, logger)
        {
            _scopeFactory = scopeFactory;
        }

        public override Task PublishAsync(IntegrationEvent @event)
        {
            var eventName = @event.GetEventKey();
            var _task = ProcessingEventAsync(eventName, @event);
            _task.ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    Logger.LogError(task.Exception, "Error handling integration event: {EventName}, {Message}", eventName, task.Exception?.Message);
                }
                else // remove task from tasks list
                {
                    lock (_tasks)
                    {
                        var managedTask = _tasks.FirstOrDefault(t => t.Id == task.Id);
                        if (managedTask != null)
                        {
                            _tasks.Remove(managedTask);
                        }
                    }
                }
            })
            ;
            lock (_tasks)
            {
                _tasks.Add(_task);
            }
            return Task.CompletedTask;
        }

        protected virtual async Task ProcessingEventAsync(string eventName, IntegrationEvent @event)
        {
            using (Logger.BeginScope($"Processing integration event: {eventName} {@event.Id}"))
            {

                if (SubsManager.HasSubscriptionsForEvent(eventName))
                {
                    using var scope = _scopeFactory.CreateScope();
                    var subscriptions = SubsManager.GetHandlersForEvent(eventName);
                    foreach (var subscription in subscriptions)
                    {
                        var handler = scope.ServiceProvider.GetService(subscription.HandlerType);
                        if (handler == null) {
                            Logger.LogWarning("Handler {HandlerType} was not registerd", subscription.HandlerType.Name);
                            continue; }
                        var eventType = SubsManager.GetEventTypeByName(eventName);
                        if (eventType == null) {
                            Logger.LogWarning("Event type was not registerd for {EventName}", eventName);
                            continue; }
                        try
                        {
                            var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
                            var method = concreteType.GetMethod("HandleAsync")!;
                            // It worked but not sure if it's the best way to do it
                            await (Task)method.Invoke(handler, new object[] { @event })!;
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "Error processing integration event: {EventName}, {Message}", eventName, ex.Message);
                        }
                    }
                }
                else if (Logger.IsEnabled(LogLevel.Trace))
                {
                    Logger.LogWarning("No subscription for integration event: {EventName}", eventName);
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    lock (_tasks)
                    {
                        Task.WhenAll(_tasks).GetAwaiter().GetResult();
                    }
                    _tasks.Clear();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~InMemoryEventBus()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
