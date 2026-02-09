namespace Juice
{
    public abstract record MessageBase : IMessage
    {
        public virtual Guid MessageId { get; init; } = Guid.NewGuid();

        public virtual DateTimeOffset CreatedAt { get;init; } = DateTimeOffset.UtcNow;

        public virtual string? TenantId { get; protected set; }

        public MessageBase()
        {
        }
        public MessageBase(Guid id)
        {
            MessageId = id;
        }
    }
}
