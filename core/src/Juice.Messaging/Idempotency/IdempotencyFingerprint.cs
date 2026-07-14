using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Juice.Messaging.Idempotency
{
    /// <summary>
    /// Computes a stable fingerprint of a command/request payload for idempotency conflict detection
    /// (FR-005): reuse of a key with a materially different payload yields a different hash, which the
    /// store reports as <see cref="IdempotencyOutcome.Conflict"/>.
    /// <para></para>
    /// Volatile envelope fields that change on every message instance (see <see cref="VolatileFields"/>)
    /// are stripped before hashing so that legitimate retries of the same logical operation hash
    /// identically — otherwise a fresh <c>MessageId</c>/<c>CreatedAt</c> per retry would falsely register
    /// as a conflict and break idempotency.
    /// </summary>
    public static class IdempotencyFingerprint
    {
        /// <summary>
        /// Root-level fields removed before hashing because they differ per instance (see
        /// <c>MessageBase.MessageId</c> / <c>MessageBase.CreatedAt</c>). Only the business payload
        /// should influence the fingerprint.
        /// </summary>
        public static readonly string[] VolatileFields = { "MessageId", "CreatedAt" };

        /// <summary>
        /// Compute a hex SHA-256 fingerprint of <paramref name="payload"/>, or <c>null</c> when the
        /// payload is null/empty. Stores that do not persist a fingerprint ignore the returned value.
        /// </summary>
        public static string? Compute(object? payload, IMessageSerializer serializer)
        {
            if (payload is null)
            {
                return null;
            }

            var serialized = serializer.Serialize(payload);
            if (string.IsNullOrEmpty(serialized))
            {
                return null;
            }

            var canonical = StripVolatileFields(serialized);
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
            return Convert.ToHexString(bytes);
        }

        private static string StripVolatileFields(string serialized)
        {
            try
            {
                if (JToken.Parse(serialized) is JObject root)
                {
                    foreach (var field in VolatileFields)
                    {
                        root.Remove(field);
                    }
                    return root.ToString(Formatting.None);
                }
            }
            catch (JsonException)
            {
                // Not JSON we can normalize — fall back to hashing the raw payload.
            }
            return serialized;
        }
    }
}
