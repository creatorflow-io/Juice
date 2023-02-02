using System.Text.RegularExpressions;

namespace Juice.EventBus.RabbitMQ
{
    public static class RabbitMQUtils
    {
        public static bool IsTopicMatch(string eventRoutingKey, string consumeRoutingKey)
        {
            return Regex.IsMatch(eventRoutingKey, "^" + consumeRoutingKey.Replace(".", "\\.").Replace("*", "([^\\.]+){1}")
                 .Replace("\\.#", "(\\.[^\\.]+)*").Replace("#\\.", "([^\\.]+\\.)*") + "$");
        }
    }
}
