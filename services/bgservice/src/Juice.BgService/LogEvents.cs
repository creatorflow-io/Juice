using Microsoft.Extensions.Logging;

namespace Juice.BgService
{
    public class LogEvents
    {
        public static EventId ServiceInvokeFailed = new EventId(2401, nameof(ServiceInvokeFailed));
    }
}
