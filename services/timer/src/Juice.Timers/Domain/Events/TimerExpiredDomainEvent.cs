namespace Juice.Timers.Domain.Events
{
    public class TimerExpiredDomainEvent : INotification
    {
        public TimerRequest Request { get; private set; }
        public TimerExpiredDomainEvent(TimerRequest request)
        {
            Request = request;
        }
    }
}
