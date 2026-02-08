
namespace Juice.Messaging
{
    public static class MessageLogContext
    {
        public static object ToLogObject(
            string action,
            string? messageId = null,
            string? messageName = null)
        {
            if (!MessageContext.IsInitialized)
            {
                return new { action };
            }

            var ctx = MessageContext.Current;

            return new
            {
                action,
                correlationId = ctx.CorrelationId,
                executionId = ctx.ExecutionId,
                causationId = ctx.CausationId,
                source = ctx.Source,
                messageId,
                messageName
            };
        }
    }
}
