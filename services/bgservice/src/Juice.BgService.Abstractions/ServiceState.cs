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
        /// <summary>
        /// Service is working properly
        /// </summary>
        Running = 5,
        Suspending = 6,
        Suspended = 7,
        Resuming = 8,
        Stopping = 9,
        Stopped = 10,
        StoppedUnexpectedly = 11,
        RestartPending = 12,
        Restarting = 13
    }

    public static class ServiceStateExtensions
    {
        private static readonly ServiceState[] _workingStates = new ServiceState[] {
            ServiceState.Starting,
            ServiceState.Waiting,
            ServiceState.Scheduled,
            ServiceState.Processing,
            ServiceState.Running,
            ServiceState.Resuming,
            ServiceState.RestartPending,
            ServiceState.Restarting
        };


        private static readonly ServiceState[] _failureStates = new ServiceState[] {
            ServiceState.StoppedUnexpectedly
        };

        public static bool IsInWorkingStates(this ServiceState state)
        {
            return _workingStates.Contains(state);
        }

        public static bool IsInFailureStates(this ServiceState state)
        {
            return _failureStates.Contains(state);
        }
    }
}
