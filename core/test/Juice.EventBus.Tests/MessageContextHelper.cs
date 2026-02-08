using Juice.Messaging;
using Juice.Services;

namespace Juice.EventBus.Tests
{
    public class MessageContextHelper
    {
        public static void InitMessageContext()
        {
            MessageContext.Initialize(
                correlationId: StringIdGenerator.Instance.GenerateUniqueId(),
                causationId: null,
                executionId: StringIdGenerator.Instance.GenerateUniqueId(),
                source: "xunit.test"
            );
        }
    }
}
