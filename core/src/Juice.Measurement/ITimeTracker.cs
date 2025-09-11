namespace Juice.Measurement
{
    public interface ITimeTracker : IDisposable
    {
        TimeSpan ElapsedTime { get; }
        /// <summary>
        /// Execution records.
        /// </summary>
        List<ITrackRecord> Records { get; }

        /// <summary>
        /// Create a new scope to tracking time.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="scopeId"></param>
        /// <returns></returns>
        IDisposable BeginScope(string name, string? scopeId = default);

        /// <summary>
        /// Print the elapsed time from begin scope.
        /// </summary>
        /// <param name="name"></param>
        void Checkpoint(string name);

        /// <summary>
        /// Print the execution records in a table.
        /// </summary>
        /// <param name="humanReadable"></param>
        /// <param name="maxDepth"></param>
        /// <param name="checkpoint"></param>
        /// <returns></returns>
        string ToString(bool humanReadable, int? maxDepth = default, bool checkpoint = true);

        /// <summary>
        /// Get all scope records.
        /// </summary>
        /// <returns></returns>
        ICollection<ScopeRecord> GetScopes();
    }
}
