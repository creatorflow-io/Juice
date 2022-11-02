using Juice.BgService.Extensions;
using Juice.Extensions;
using Juice.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Juice.BgService.Management
{
    public class ServiceManager : BackgroundService, IManagedService<IServiceModel>
    {
        private readonly IServiceStore _serviceStore;
        private readonly IServiceFactory _serviceFactory;

        public List<IManagedService> ManagedServices => _services;
        private List<IManagedService> _services = new List<IManagedService>();

        private readonly IOptionsMutable<ServiceManagerOptions> _options;

        public override Guid Id => _options.Value.Id;

        public ServiceManager(
            IOptionsMutable<ServiceManagerOptions> options,
            ILogger<ServiceManager> logger,
            IServiceFactory serviceFactory,
            IServiceStore serviceStore) : base(logger)
        {
            _options = options;
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
                    _logger.FailedToInvoke(ex.Message, ex);
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

                foreach (var service in services.Where(s => !s.Id.HasValue || s.Id.Value != Id))
                {
                    var threads = service.Options?.GetOption<int>("Threads");
                    var startType = service.Options?.GetOption<StartupType>("StartupType") ?? StartupType.Auto;
                    threads = Math.Max(1, Math.Min(threads ?? 0, 10));
                    Type? serviceType = default;
                    try
                    {
                        serviceType = Type.GetType(service.AssemblyQualifiedName);
                    }
                    catch (Exception ex)
                    {
                        Logging($"Failed to load type {service.AssemblyQualifiedName}. {ex.Message}", LogLevel.Error);
                    }
                    if (serviceType == null)
                    {
                        Logging($"Type {service.AssemblyQualifiedName} not found", LogLevel.Error);
                        continue;
                    }
                    for (var i = 0; i < threads; i++)
                    {
                        var bgService = _serviceFactory.CreateService(serviceType);

                        if (bgService != null && bgService is IManagedService managedService)
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
                                managedService.SetDescription(service.Name + " " + i.ToString());
                            }
                            else
                            {
                                managedService.SetDescription(service.Name);
                            }
                            #endregion

                            _services.Add(managedService);

                            #region Startup
                            if (startType == StartupType.Auto)
                            {
                                await managedService.StartAsync(default);
                                Logging($"Service <a data-type='service' data-id='{managedService.Id}' href='#'>{managedService.Description}</a> started.");
                            }
                            else
                            {
                                if (startType == StartupType.Delayed)
                                {
                                    Task.Run(async () =>
                                    {
                                        await Task.Delay(5000);
                                        await managedService.StartAsync(default(CancellationToken));
                                        Logging($"Service <a data-type='service' data-id='{managedService.Id}' href='#'>{managedService.Description}</a> started after 5s.");
                                    });
                                }
                                else
                                {
                                    Logging($"Service <a data-type='service' data-id='{managedService.Id}' href='#'>{managedService.Description}</a> is not started");
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

        protected override void Cleanup()
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
    }
}
