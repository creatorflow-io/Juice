using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Juice.Timers.Domain.AggregratesModel.TimerAggregrate
{
    public class TimerRequest : AggregrateRoot<INotification>
    {
        public TimerRequest() { }
        public TimerRequest(string issuer, string correlationId, DateTimeOffset absoluteExpiried)
        {
            Issuer = issuer;
            CorrelationId = correlationId;
            AbsoluteExpired = absoluteExpiried;
        }

        [Key]
        public Guid Id { get; init; }
        public string Issuer { get; init; }
        public string CorrelationId { get; init; }
        public DateTimeOffset AbsoluteExpired { get; init; }


        public bool IsCompleted { get; private set; }

        public DateTimeOffset? ProcessedTime { get; private set; }

        [NotMapped]
        public bool IsExpired => AbsoluteExpired < DateTimeOffset.Now;

        #region Methods

        public void Complete()
        {
            if (!IsCompleted)
            {
                IsCompleted = true;
                ProcessedTime = DateTimeOffset.Now;
                this.AddDomainEvent(new TimerExpiredDomainEvent(this));
            }
        }

        #endregion
    }
}
