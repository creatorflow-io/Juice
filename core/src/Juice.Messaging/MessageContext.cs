using Juice.Messaging.Context;

namespace Juice.Messaging
{
    public static class MessageContext
    {
        private static readonly AsyncLocal<MessageContextData?> _current = new();

        public static bool IsInitialized => _current.Value != null;

        public static MessageContextData Current =>
            _current.Value
            ?? throw new InvalidOperationException(
                "MessageContext is not initialized. " +
                "It must be initialized at an entry point.");

        public static void Initialize(
            string correlationId,
            string? causationId,
            string executionId,
            string source)
        {
            _current.Value = new MessageContextData(
                correlationId,
                causationId,
                executionId,
                source);
        }

        public static void Clear()
        {
            _current.Value = null;
        }

        public static void InitializeTestContext()
            => Initialize(
                correlationId: Guid.NewGuid().ToString(),
                causationId: null,
                executionId: Guid.NewGuid().ToString(),
                source: "test-source");
    }
}
