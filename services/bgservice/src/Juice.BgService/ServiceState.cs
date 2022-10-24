namespace Juice.BgService
{
    /// <summary>
    /// State of hosted service
    /// </summary>
    public enum ServiceState
    {
        Empty = 0,
        /// <summary>
        /// Service is starting
        /// </summary>
        Starting = 1,
        /// <summary>
        /// Service is free and waiting for new job (passive mode)
        /// </summary>
        Waiting = 2,
        /// <summary>
        /// Service is free and wait until the scheduled time to next processing (active mode)
        /// </summary>
        Scheduled = 3,
        /// <summary>
        /// Service is processing a job
        /// </summary>
        Processing = 4,
        Suspending = 5,
        Suspended = 6,
        Resuming = 7,
        Stopping = 8,
        Stopped = 9,
        StoppedUnexpectedly = 10,
        RestartPending = 11,
        Restarting = 12
    }

    public static class ServiceStateExtensions
    {
        private static ServiceState[] _workingStates = new ServiceState[] {
            ServiceState.Starting,
            ServiceState.Waiting,
            ServiceState.Scheduled,
            ServiceState.Processing,
            ServiceState.Resuming,
            ServiceState.RestartPending,
            ServiceState.Restarting
        };

        public static bool IsInWorkingStates(this ServiceState state)
        {
            return _workingStates.Contains(state);
        }
    }
}
