namespace Juice.BgService.Management.Extensions
{
    public static class ServiceManagerExtensions
    {
        private static IManagedService GetService(this ServiceManager manager, Guid id)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentNullException(nameof(id));
            }
            var service = manager.ManagedServices.Where(s => s.Id == id).FirstOrDefault();
            if (service == null)
            {
                throw new KeyNotFoundException($"Service with Id {id} could not be found");
            }
            return service;
        }

        public static Task StartAsync(this ServiceManager manager, Guid id, CancellationToken cancellationToken)
            => manager.GetService(id).StartAsync(cancellationToken);

        public static Task StopAsync(this ServiceManager manager, Guid id, CancellationToken cancellationToken)
            => manager.GetService(id).StopAsync(cancellationToken);

        public static Task RequestStopAsync(this ServiceManager manager, Guid id, CancellationToken cancellationToken)
            => manager.GetService(id).RequestStopAsync(cancellationToken);

        public static Task RestartAsync(this ServiceManager manager, Guid id, CancellationToken cancellationToken)
            => manager.GetService(id).RestartAsync(cancellationToken);

        public static Task RequestRestartAsync(this ServiceManager manager, Guid id, CancellationToken cancellationToken)
            => manager.GetService(id).RequestRestartAsync(cancellationToken);
    }
}
