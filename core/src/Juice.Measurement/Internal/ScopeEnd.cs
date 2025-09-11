namespace Juice.Measurement.Internal
{
    public record ScopeEnd(string Name, string FullName, int Depth, TimeSpan RecordTime, TimeSpan ElapsedTime, string? ScopeId)
        : ITrackRecord, IScope;
}
