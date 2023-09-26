using Juice.Extensions;
using Juice.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Juice.BgService.Management
{
    public class ServiceManager : ManagedService, IManagedService<IServiceModel>, IHostedService
    {
        private readonly IServiceRepository _serviceStore;
        private readonly IServiceFactory _serviceFactory;

        public List<IManagedService> ManagedServices => _services;
        private List<IManagedService> _services = new List<IManagedService>();

        private readonly IOptionsMutable<ServiceManagerOptions> _options;

        private ILogger _logger;

        public override Guid Id => _options.Value.Id;

        public ServiceManager(
            IOptionsMutable<ServiceManagerOptions> options,
            ILogger<ServiceManager> logger,
            IServiceFactory serviceFactory,
            IServiceRepository serviceStore) : base()
        {
            _options = options;
            _logger = logger;
            _serviceStore = serviceStore;
            _serviceStore.OnChanged += ServiceStore_OnChanged;
            _serviceFactory = serviceFactory;
        }

        #region Events

        private void ServiceStore_OnChanged(object? sender, EventArgs e)
        {
            Logging("Service store changed");
            this.RequestRestartAsync(default).Wait();
        }

        #endregion

        #region Controls

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            if (Id == Guid.Empty)
            {
                await _options.UpdateAsync(o => o.Id = Guid.NewGuid());
            }
            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync()
        {
            State = ServiceState.Running;
            var startOfRun = true;

            if (_shutdown == null)
            {
                _shutdown = new CancellationTokenSource();
            }
            if (_stopRequest == null)
            {
                _stopRequest = new CancellationTokenSource();
            }

            while (!_shutdown.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(3000);

                    #region monitor service's health check to restart not healthy services
                    foreach (var service in ManagedServices)
                    {
                        try
                        {
                            var (healthy, message) = await service.HealthCheckAsync();
                            if (!healthy)
                            {
                                Logging($"Service {service.Description} reported {message ?? "NotHealthy"} and must restart");
                                await service.RestartAsync(default(CancellationToken));
                            }
                        }
                        catch (NotImplementedException) { }
                    }
                    #endregion

                    if (ManagedServices.Any())
                    {
                        if (ManagedServices.All(s => s.State == ServiceState.Stopped
                            || s.State == ServiceState.StoppedUnexpectedly
                            || s.State == ServiceState.Empty // service was not started
                            ))
                        {
                            Message = "All services was stopped.";
                            if (State == ServiceState.Stopping || State == ServiceState.RestartPending)
                                break;
                        }
                        else if (ManagedServices.Any(s => s.State == ServiceState.Running))
                        {
                            State = ServiceState.Running;
                        }
                    }
                    if ((!ManagedServices.Any()) && !_stopRequest.IsCancellationRequested)
                    {
                        if (!startOfRun)
                        {
                            await Task.Delay(20000, _stopRequest.Token);
                        }
                        startOfRun = false;
                        await InitServicesAsync();
                    }
                }
                catch (TaskCanceledException ex)
                {

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, ex.Message);
                }
            }
            if (_stopRequest.IsCancellationRequested
                || State == ServiceState.Stopping
                || State == ServiceState.Restarting
                || State == ServiceState.RestartPending)
            {
                State = ServiceState.Stopped;
            }
            else
            {
                State = ServiceState.StoppedUnexpectedly;
            }
        }

        public override async Task RequestStopAsync(CancellationToken cancellationToken)
        {
            foreach (var service in _services)
            {
                await service.RequestStopAsync(cancellationToken);
            }
            await base.RequestStopAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var service in _services)
            {
                await service.StopAsync(cancellationToken);
            }
            await base.StopAsync(cancellationToken);
        }

        #endregion
        private async Task InitServicesAsync()
        {
            Logging($"Trying to receive services model");

            var services = await _serviceStore.GetServicesModelAsync(_shutdown.Token);

            if (services != null)
            {
                if (services.Any(s => s.Id.HasValue && s.Id == Id))
                {
                    var service = services.First(s => s.Id.HasValue && s.Id == Id);
                    Configure(service);
                }

                var pendingTasks = new List<Task>();
                foreach (var service in services.Where(s => !s.Id.HasValue || s.Id.Value != Id))
                {
                    var threads = service.Options?.GetOption<int>("Threads");
                    var startType = service.Options?.GetOption<StartupType>("StartupType") ?? StartupType.Auto;
                    threads = Math.Max(1, Math.Min(threads ?? 0, 10));

                    if (!_serviceFactory.IsServiceExists(service.AssemblyQualifiedName))
                    {
                        Logging($"Type {service.AssemblyQualifiedName} not found", LogLevel.Error);
                        continue;
                    }
                    for (var i = 0; i < threads; i++)
                    {
                        var bgService = _serviceFactory.CreateService(service.AssemblyQualifiedName);

                        if (bgService != null)
                        {
                            #region Configure
                            if (bgService is IManagedService<IServiceModel> configurableService)
                            {
                                configurableService.Configure(service);
                            }
                            #endregion

                            #region Description
                            if (threads > 1)
                            {
                                bgService.SetDescription(service.Name + " " + i.ToString());
                            }
                            else
                            {
                                bgService.SetDescription(service.Name);
                            }
                            #endregion

                            _services.Add(bgService);

                            #region Startup
                            if (startType == StartupType.Auto)
                            {
                                await bgService.StartAsync(default);
                                Logging($"Service <a data-type='service' data-id='{bgService.Id}' href='#'>{bgService.Description}</a> started.");
                            }
                            else
                            {
                                if (startType == StartupType.Delayed)
                                {
                                    var task = Task.Run(async () =>
                                    {
                                        await Task.Delay(5000);
                                        await bgService.StartAsync(default(CancellationToken));
                                        Logging($"Service <a data-type='service' data-id='{bgService.Id}' href='#'>{bgService.Description}</a> started after 5s.");
                                    });
                                    pendingTasks.Add(task);
                                }
                                else
                                {
                                    Logging($"Service <a data-type='service' data-id='{bgService.Id}' href='#'>{bgService.Description}</a> is not started");
                                }
                            }
                            #endregion
                        }
                        else
                        {
                            Logging($"Service {service.AssemblyQualifiedName} does not registered in service collection", LogLevel.Error);
                        }
                    }
                }

                Task.WaitAll(pendingTasks.ToArray());
            }
        }

        protected void CleanupManagedServices()
        {
            if (_services.Any())
            {
                foreach (var service in _services)
                {
                    try
                    {
                        if (service.State.IsInWorkingStates())
                        {
                            service.StopAsync(default).Wait();
                        }

                        service.Dispose();
                        var a = service.State;
                    }
                    catch (Exception ex)
                    {
                        Logging(ex.Message, LogLevel.Error);
                    }
                }
                _services.Clear();
            }
        }

        protected void Cleanup()
        {
            CleanupManagedServices();
            try
            {
                _stopRequest?.Dispose();
                _shutdown?.Dispose();
                _backgroundTask?.Dispose();
            }
            catch (Exception) { }
            GC.Collect();
        }
        public override Task<(bool Healthy, string Message)> HealthCheckAsync() => throw new NotImplementedException();
        public void Configure(IServiceModel model)
        {
            Description = model.Name;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Cleanup();
            }
            base.Dispose(disposing);
        }

        public void Logging(string message, LogLevel level = LogLevel.Information) { }

    }
}
