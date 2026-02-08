namespace Juice
{
    public abstract record Message : IMessage
    {
        public virtual Guid MessageId { get; init; } = Guid.NewGuid();

        public virtual DateTimeOffset CreatedAt { get;init; } = DateTimeOffset.UtcNow;

        public virtual string? TenantId { get; protected set; }

        public Message()
        {
        }
        public Message(Guid id)
        {
            MessageId = id;
        }
    }
}
