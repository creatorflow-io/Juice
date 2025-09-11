namespace Juice.Measurement
{
    public interface ITrackRecord
    {
        /// <summary>
        /// The name of the scope or checkpoint.
        /// </summary>
        string Name { get; }
        /// <summary>
        /// The full name of the scope or checkpoint.
        /// </summary>
        string FullName { get; }
        /// <summary>
        /// The depth of the scope or checkpoint.
        /// </summary>
        int Depth { get; }
        /// <summary>
        /// The time from the beginning of the tracking.
        /// </summary>
        TimeSpan RecordTime { get; }
        /// <summary>
        /// Elapsed time from the beginning of the scope or the last checkpoint.
        /// </summary>
        TimeSpan ElapsedTime { get; }
    }
}
