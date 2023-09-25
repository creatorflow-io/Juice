namespace Juice.BgService
{
    public class ServiceEventArgs : EventArgs
    {
        public ServiceEventArgs(string name, Guid serviceId, string serviceName, ServiceState state, string? message, string? data)
        {
            ServiceId = serviceId;
            ServiceName = serviceName;
            State = state;
            Message = message;
            Data = data;
            EventName = name;
        }
        public string EventName { get; private set; }
        public Guid ServiceId { get; private set; }
        public string ServiceName { get; private set; }
        public ServiceState State { get; private set; }
        /// <summary>
        /// Short message
        /// </summary>
        public string? Message { get; private set; }
        /// <summary>
        /// Details
        /// </summary>
        public string? Data { get; private set; }
    }
}
