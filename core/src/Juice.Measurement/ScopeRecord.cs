namespace Juice.Measurement
{
    public record ScopeRecord(string Name, string FullName, int Depth, TimeSpan StartedTime, TimeSpan ElapsedTime, string ScopeId);
}
