using System.Globalization;
using System.Text;
using NATS.Client.Core;

namespace Juice.EventBus.NATS
{
    /// <summary>
    /// NATS headers are strings only. See wire-format.md, Headers section.
    /// </summary>
    internal static class JetStreamHeaders
    {
        public static NatsHeaders Encode(IDictionary<string, object?>? headers)
        {
            var result = new NatsHeaders();
            if (headers == null)
            {
                return result;
            }
            foreach (var header in headers)
            {
                var encoded = EncodeValue(header.Value);
                if (encoded != null)
                {
                    result[header.Key] = encoded;
                }
            }
            return result;
        }

        public static Dictionary<string, object?> Decode(NatsHeaders? headers)
        {
            var result = new Dictionary<string, object?>();
            if (headers == null)
            {
                return result;
            }
            foreach (var header in headers)
            {
                result[header.Key] = header.Value.Count > 0 ? header.Value[0] : null;
            }
            return result;
        }

        private static string? EncodeValue(object? value)
            => value switch
            {
                null => null,
                string s => s,
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
                DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()
            };
    }
}
