namespace Juice.Messaging.Internal
{
    internal sealed class NodeIdentity : INodeIdentity
    {
        public string NodeId { get; }

        public NodeIdentity()
        {
            var nodeId = $"{Environment.MachineName}:{Environment.ProcessId}";
            if (nodeId.Length > NodeIdentityValidator.MaxLength)
            {
                throw new InvalidOperationException(
                    $"Computed node identity '{nodeId}' exceeds the maximum allowed length of {NodeIdentityValidator.MaxLength} characters. " +
                    $"Use MessagingBuilder.UseNodeIdentity(string) to supply a shorter identifier.");
            }
            NodeId = nodeId;
        }
    }
}
