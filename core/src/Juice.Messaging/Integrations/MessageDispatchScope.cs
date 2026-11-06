namespace Juice.Messaging.Integrations
{
    /// <summary>
    /// Whether the current flow is an integration event handler. A message can be retried, which an HTTP caller's repeat is
    /// not: code that stores an outcome for replay needs to know which it is serving.
    /// </summary>
    public static class MessageDispatchScope
    {
        private static readonly AsyncLocal<bool> _active = new();

        public static bool IsActive => _active.Value;

        /// <summary>Marks the flow until the result is disposed.</summary>
        public static IDisposable Enter()
        {
            var previous = _active.Value;
            _active.Value = true;
            return new Restore(previous);
        }

        private sealed class Restore(bool previous) : IDisposable
        {
            public void Dispose() => _active.Value = previous;
        }
    }
}
