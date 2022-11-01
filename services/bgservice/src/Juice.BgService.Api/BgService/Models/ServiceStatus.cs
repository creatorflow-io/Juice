namespace Juice.BgService.Api.BgService.Models
{
    public record ServiceStatus
    {
        public ServiceStatus(string name, ServiceState state, string? message, IEnumerable<IManagedService>? services)
        {
            Name = name;
            State = state;
            Message = message;
            Services = services?.Select(s => new ServiceStatus(s.Description, s.State, s.Message, default));
        }
        public string Name { get; init; }
        public ServiceState State { get; init; }
        public string? Message { get; init; }
        public IEnumerable<ServiceStatus>? Services { get; init; }
    }
}
