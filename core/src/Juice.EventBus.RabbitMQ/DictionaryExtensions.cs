using System.Text;
using RabbitMQ.Client;

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

        /// <summary>
        /// Returns a new dictionary where values not supported by the AMQP 0-9-1 table spec
        /// are converted to their string representation.
        /// Supported types: bool, byte, sbyte, short, ushort, int, uint, long, ulong,
        /// float, double, decimal, string, byte[], AmqpTimestamp, BinaryTableValue,
        /// IList&lt;object?&gt;, IDictionary&lt;string, object?&gt;, null.
        /// </summary>
        public static IDictionary<string, object?> ToStandardized(this IDictionary<string, object?>? dictionary)
        {
            if (dictionary == null)
            {
                return new Dictionary<string, object?>();
            }
            var result = new Dictionary<string, object?>(dictionary.Count);
            foreach (var kvp in dictionary)
            {
                result[kvp.Key] = IsAmqpSupported(kvp.Value) ? kvp.Value : kvp.Value?.ToString();
            }
            return result;
        }

        private static bool IsAmqpSupported(object? value) => value switch
        {
            null => true,
            bool => true,
            byte => true,
            sbyte => true,
            short => true,
            ushort => true,
            int => true,
            uint => true,
            long => true,
            ulong => true,
            float => true,
            double => true,
            decimal => true,
            string => true,
            byte[] => true,
            AmqpTimestamp => true,
            BinaryTableValue => true,
            IList<object?> => true,
            IDictionary<string, object?> => true,
            _ => false
        };
    }
}
