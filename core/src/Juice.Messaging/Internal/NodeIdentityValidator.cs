namespace Juice.Messaging.Internal
{
    internal static class NodeIdentityValidator
    {
        // Matches LengthConstants.NameLength (256) — kept local to avoid a transitive
        // project reference from Juice.Messaging to Juice.Contracts.
        internal const int MaxLength = 256;

        internal static void Validate(string nodeId, string paramName)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
                throw new ArgumentException("Node identity must not be null or empty.", paramName);

            if (nodeId.Length > MaxLength)
                throw new ArgumentException(
                    $"Node identity '{nodeId}' exceeds the maximum allowed length of {MaxLength} characters.",
                    paramName);
        }
    }
}
