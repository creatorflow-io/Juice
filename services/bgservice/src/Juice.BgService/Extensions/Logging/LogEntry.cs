namespace Juice.BgService.Extensions.Logging
{
    internal record LogEntry
    {
        public LogEntry(DateTimeOffset logTime, string cateogry, string message)
        {
            Timestamp = logTime;
            Message = message;
            Category = cateogry;
        }
        public DateTimeOffset Timestamp { get; init; }
        public string Message { get; init; }
        public string Category { get; init; }
        public string? FileName { get; protected set; }
        public string? State { get; protected set; }
        public List<LogScope>? Scopes { get; protected set; }
        public void PushScope(LogScope scope)
        {
            if (Scopes == null)
            {
                Scopes = new List<LogScope>();
            }
            Scopes.Add(scope);
        }
        public void ForkNewFile(string name)
        {
            FileName = name;
        }
        public void SetState(string? state)
        {
            State = state;
        }
    }

    internal class LogScope
    {
        public string? Scope { get; init; }
        public Dictionary<string, object>? Properties { get; set; }
    }
}
