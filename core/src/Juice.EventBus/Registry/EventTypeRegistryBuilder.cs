using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.EventBus.Registry
{
    public sealed class EventTypeRegistryBuilder
    {
        private readonly List<Type> _eventTypes = new();
        public IReadOnlyList<Type> EventTypes => _eventTypes.AsReadOnly();
        public void Register<TEvent>()
            where TEvent : class, IIntegrationEvent
        {
            var eventType = typeof(TEvent);
            if (!_eventTypes.Contains(eventType))
            {
                _eventTypes.Add(eventType);
            }
        }
        public void RegisterEventsFromAssemblyContaining<T>(bool? includeNonPublicTypes = default)
        {
            var assembly = typeof(T).Assembly;
            RegisterEventsFromAssemblyInternal(assembly, includeNonPublicTypes ?? assembly == Assembly.GetCallingAssembly());
        }

        public void RegisterEventsFromAssemblyContaining(Type type, bool? includeNonPublicTypes = default)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            var assembly = type.Assembly;
            RegisterEventsFromAssemblyInternal(assembly, includeNonPublicTypes ?? assembly == Assembly.GetCallingAssembly());
        }

        public void RegisterEventsFromAssembly(Assembly assembly, bool? includeNonPublicTypes = default)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            RegisterEventsFromAssemblyInternal(assembly, includeNonPublicTypes ?? assembly == Assembly.GetCallingAssembly());
        }

        private void RegisterEventsFromAssemblyInternal(Assembly assembly, bool includeNonPublicTypes)
        {
            var types = assembly.GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .Where(t => t.IsPublic || includeNonPublicTypes)
                .Where(t => typeof(IIntegrationEvent).IsAssignableFrom(t));
            _eventTypes.AddRange(types);
        }

        public IEventTypeRegistry Build(IServiceProvider sp)
        {
            var logger = sp.GetRequiredService<ILogger<EventTypeRegistry>>();
            return new EventTypeRegistry([.. _eventTypes.Distinct()], logger);
        }
    }
}
