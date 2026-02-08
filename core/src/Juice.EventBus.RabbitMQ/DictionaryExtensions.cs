using System.Text;

namespace Juice.EventBus.RabbitMQ
{
    internal static class DictionaryExtensions
    {
        public static string? GetHeaderString(this IDictionary<string, object?>? dictionary, string key)
        {
            if (dictionary == null)
            {
                return default;
            }
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            if (dictionary.TryGetValue(key, out var value))
            {
                return value is byte[] bytes
                        ? UTF8Encoding.UTF8.GetString(bytes)
                        : value?.ToString();
            }
            return default;
        }

        public static int? GetHeaderInt(this IDictionary<string, object?>? dictionary, string key)
        {
            if (dictionary == null)
            {
                return default;
            }
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            if (dictionary.TryGetValue(key, out var value))
            {
                if (value is byte[] bytes)
                {
                    var str = UTF8Encoding.UTF8.GetString(bytes);
                    if (int.TryParse(str, out var intValue))
                    {
                        return intValue;
                    }
                }
                else if (value is int intValue)
                {
                    return intValue;
                }
                else if (value != null)
                {
                    var str = value.ToString();
                    if (int.TryParse(str, out intValue))
                    {
                        return intValue;
                    }
                }
            }
            return null;
        }
    }
}
