namespace Juice.Measurement.Internal
{
    public record ScopeStart(string Name, string FullName, int Depth, TimeSpan RecordTime, TimeSpan ElapsedTime, string? ScopeId)
        : ITrackRecord, IScope;
}
