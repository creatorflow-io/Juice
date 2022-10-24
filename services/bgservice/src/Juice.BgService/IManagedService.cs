using Microsoft.Extensions.Hosting;

namespace Juice.BgService
{
    public interface IManagedService : IHostedService
    {
        ServiceState State { get; }
        event EventHandler<ServiceEventArgs> OnChanged;

        Task RestartAsync(CancellationToken cancellationToken);

        Task RequestStopAsync(CancellationToken cancellationToken);

        Task RequestRestartAsync(CancellationToken cancellationToken);

    }

    public interface IManagedService<TModel> : IManagedService
        where TModel : IServiceModel
    {
        void Configure(TModel model);
    }
}
