using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Juice.EventBus.IntegrationEventLog.EF
{
    public class IntegrationEventLogEntry
    {
        /// <summary>
        /// Parameterless constructor for EFCore binding
        /// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        private IntegrationEventLogEntry() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public IntegrationEventLogEntry(IntegrationEvent @event, Guid transactionId)
        {
            EventId = @event.Id;
            CreationTime = @event.CreationDate;
            EventTypeName = @event.GetType().FullName!;
            Content = JsonConvert.SerializeObject(@event);
            State = EventState.NotPublished;
            TimesSent = 0;
            TransactionId = transactionId.ToString();
        }
        public Guid EventId { get; private set; }
        public string EventTypeName { get; private set; }
        [NotMapped]
        public string EventTypeShortName => EventTypeName.Split('.').Last();
        [NotMapped]
        public IntegrationEvent? IntegrationEvent { get; private set; }
        public EventState State { get; private set; }
        public int TimesSent { get; private set; }
        public DateTime? ModificationTime { get; private set; }
        public DateTime CreationTime { get; private set; }
        public string Content { get; private set; }
        public string TransactionId { get; private set; }

        public IntegrationEventLogEntry DeserializeJsonContent(Type? type)
        {
            if(type == null)
            {
                return this;
            }
            IntegrationEvent = JsonConvert.DeserializeObject(Content, type) as IntegrationEvent;
            return this;
        }

        public void UpdateState(EventState state)
        {
            if (state == EventState.InProgress)
            {
                TimesSent++;
            }
            State = state;
            ModificationTime = DateTime.UtcNow;
        }
    }
}
