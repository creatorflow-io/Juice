namespace Juice
{
    public interface IEvent : IMessage
    {
        string EventName { get; }
    }
}
