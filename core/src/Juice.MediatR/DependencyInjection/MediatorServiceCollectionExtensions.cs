using System.Reflection;
using Juice.MediatR;
using Juice.MediatR.Internal;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class MediatorServiceCollectionExtensions
    {
        public static IServiceCollection AddMediatR(this IServiceCollection services, Action<MediatorBuilder>? buildAction = default)
        {
            services.TryAddTransient<IMediator, Mediator>();
            var builder = new MediatorBuilder(services);
            buildAction?.Invoke(builder);
            return services;
        }
    }

    public class MediatorBuilder
    {
        public IServiceCollection Services { get; }
        public MediatorBuilder(IServiceCollection services)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public void RegisterServicesFromAssemblyContaining<T>(bool? includeNonPublicTypes = default)
        {
            var assembly = typeof(T).Assembly;
            RegisterServicesFromAssemblyInternal(assembly, includeNonPublicTypes ?? assembly == Assembly.GetCallingAssembly());
        }

        public void RegisterServicesFromAssemblyContaining(Type type, bool? includeNonPublicTypes = default)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            var assembly = type.Assembly;
            RegisterServicesFromAssemblyInternal(assembly, includeNonPublicTypes ?? assembly == Assembly.GetCallingAssembly());
        }

        public void RegisterServicesFromAssembly(Assembly assembly, bool? includeNonPublicTypes = default)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            RegisterServicesFromAssemblyInternal(assembly, includeNonPublicTypes ?? assembly == Assembly.GetCallingAssembly());
        }

        private void RegisterServicesFromAssemblyInternal(Assembly assembly, bool includeNonPublicTypes)
        {
            var types = assembly.GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .Where(t => t.IsPublic || includeNonPublicTypes)
                .ToList();


            foreach (var type in types)
            {
                var interfaces = type.GetInterfaces()
                    .Where(i => i.IsGenericType)
                    .ToList();

                foreach (var iface in interfaces)
                {
                    var def = iface.GetGenericTypeDefinition();

                    // Only handle mediator interfaces
                    if (def == typeof(IRequestHandler<>) ||
                        def == typeof(IRequestHandler<,>) ||
                        def == typeof(IStreamRequestHandler<,>))
                    {
                        if (type.IsGenericTypeDefinition)
                        {
                            // open generic registration
                            Services.TryAddTransient(def, type);
                        }
                        else
                        {
                            // closed generic registration
                            Services.TryAddTransient(iface, type);
                        }
                    }
                    // Only handle mediator interfaces
                    if (def == typeof(INotificationHandler<>) ||
                        def == typeof(IPipelineBehavior<>) ||
                        def == typeof(IPipelineBehavior<,>) ||
                        def == typeof(IStreamPipelineBehavior<,>) ||
                        def == typeof(INotificationPipelineBehavior<>))
                    {
                        if (type.IsGenericTypeDefinition)
                        {
                            // open generic registration
                            Services.AddTransient(def, type);
                        }
                        else
                        {
                            // closed generic registration
                            Services.AddTransient(iface, type);
                        }
                    }
                }
            }
        }
    }
}
