namespace Juice.Measurement.Internal
{
    public record Checkpoint(string Name, string FullName, int Depth, TimeSpan RecordTime, TimeSpan ElapsedTime) : ITrackRecord;
}
