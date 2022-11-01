using Microsoft.Extensions.Hosting;

namespace Juice.BgService
{
    public interface IManagedService : IDisposable, IHostedService
    {
        Guid Id { get; }
        string Description { get; }
        ServiceState State { get; }
        string? Message { get; }

        bool NeedStart { get; }

        event EventHandler<ServiceEventArgs> OnChanged;

        void SetState(ServiceState state, string? message, string? data = default);

        void SetDescription(string description);

        Task RequestStopAsync(CancellationToken cancellationToken);

        Task<(bool Healthy, string Message)> HealthCheckAsync();
    }

    public interface IManagedService<TModel> : IManagedService
        where TModel : IServiceModel
    {
        void Configure(TModel model);
    }

    public static class ManagedServiceExtensions
    {

        public static bool IsStopped(this IManagedService service)
            => service.State == ServiceState.Stopped || service.State == ServiceState.StoppedUnexpectedly;

        public static async Task RestartAsync(this IManagedService service, CancellationToken cancellationToken)
        {
            service.SetState(ServiceState.Restarting, "Service received a restart command.");
            await service.StopAsync(cancellationToken);
            await service.StartAsync(default(CancellationToken));
        }

        public static async Task RequestRestartAsync(this IManagedService service, CancellationToken cancellationToken)
        {
            if (service.State != ServiceState.Restarting && service.State != ServiceState.RestartPending)
            {
                if (service.IsStopped())
                {
                    await service.StartAsync(cancellationToken);
                }
                else
                {
                    service.SetState(ServiceState.RestartPending, "Service received a restart request.");
                    await service.RequestStopAsync(cancellationToken);
                }
            }
        }
    }
}
