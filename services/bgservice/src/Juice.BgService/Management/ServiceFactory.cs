namespace Juice.BgService.Management
{
    public class ServiceFactory : IServiceFactory
    {
        private IServiceProvider _serviceProvider;
        public ServiceFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }
        public IManagedService? CreateService(Type type)
        {
            if (type.IsAssignableTo(typeof(IManagedService)))
            {
                var serviceArgs = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length)
                               .First().GetParameters()
                               .Select(p => p.ParameterType.IsAssignableFrom(typeof(IServiceProvider))
                                   ? _serviceProvider : _serviceProvider.GetService(p.ParameterType))
                               .ToArray();
                return (IManagedService?)Activator.CreateInstance(type, serviceArgs);
            }
            return default;
        }
    }
}
