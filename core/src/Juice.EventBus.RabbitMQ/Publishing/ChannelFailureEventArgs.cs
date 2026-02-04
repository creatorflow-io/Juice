using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Juice.EventBus.RabbitMQ.Publishing
{
    internal sealed class ChannelFailureEventArgs : EventArgs
    {
        public string Exchange { get; }
        public ushort ReplyCode { get; }
        public string ReplyText { get; }
        public Exception? Exception { get; }
        public DateTimeOffset OccurredAt { get; }

        public ChannelFailureEventArgs(
            string exchange,
            ushort replyCode,
            string replyText,
            Exception? exception)
        {
            Exchange = exchange;
            ReplyCode = replyCode;
            ReplyText = replyText;
            Exception = exception;
            OccurredAt = DateTimeOffset.UtcNow;
        }
    }
}
