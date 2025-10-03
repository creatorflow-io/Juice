namespace Juice
{
    /// <summary>
    /// Acceptable priority values used to determine the execution order of jobs.
    /// </summary>
    public enum Priority
    {
        /// <summary>
        /// Job initially allocated to the end of the queue.
        /// </summary>
        Low = 0,

        /// <summary>
        /// Job initially allocated to be executed before any low priority jobs but after any existing medium priority jobs.
        /// </summary>
        Medium = 1,

        /// <summary>
        /// Job initially allocated before any medium and low priority jobs but after existing high priority jobs.
        /// </summary>
        High = 2,

        /// <summary>
        /// Job initially allocated to be executed before any high, medium and low priority jobs but after existing urgent jobs.
        /// </summary>
        Urgent = 3,

        /// <summary>
        /// Job should be executed as soon as the request is received.
        /// </summary>
        Immediate = 4
    }
}
