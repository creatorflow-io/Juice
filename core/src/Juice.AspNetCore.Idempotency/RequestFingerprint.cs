using System.Security.Cryptography;
using System.Text;
using Juice.Messaging;

namespace Juice.AspNetCore.Idempotency
{
    /// <summary>
    /// Computes a stable fingerprint of a request from its method, path, and a normalized body
    /// representation. Reuse of a key with a materially different payload yields a different hash,
    /// which the store reports as a conflict (FR-005).
    /// </summary>
    public static class RequestFingerprint
    {
        /// <summary>
        /// Compute a hex SHA-256 fingerprint over <c>METHOD\nPATH\nBODY</c>.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="path">Request path.</param>
        /// <param name="normalizedBody">
        /// A stable serialization of the request payload (e.g. the serialized bound command). May be null.
        /// </param>
        public static string Compute(string method, string path, string? normalizedBody)
        {
            var canonical = $"{method}\n{path}\n{normalizedBody ?? string.Empty}";
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
            return Convert.ToHexString(bytes);
        }

        /// <summary>
        /// Serialize just the payload-bearing handler arguments — excluding framework/DI values such as
        /// <see cref="Microsoft.AspNetCore.Http.HttpContext"/>, cancellation tokens, and injected services —
        /// so the fingerprint reflects the request payload, not ambient state.
        /// </summary>
        public static string SerializePayload(IEnumerable<object?> arguments, IMessageSerializer serializer)
        {
            var payload = arguments.Where(IsPayload).ToArray();
            return serializer.Serialize(payload);
        }

        private static bool IsPayload(object? arg)
        {
            if (arg is null)
            {
                return false;
            }
            var type = arg.GetType();
            if (type.IsPrimitive || arg is string || arg is decimal || arg is DateTime || arg is DateTimeOffset || arg is Guid)
            {
                return true;
            }
            if (arg is CancellationToken || arg is System.Security.Claims.ClaimsPrincipal)
            {
                return false;
            }
            var ns = type.Namespace ?? string.Empty;
            // Exclude framework/DI objects (HttpContext, services, options, etc.).
            return !ns.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
                && !ns.StartsWith("Microsoft.Extensions", StringComparison.Ordinal);
        }
    }
}
