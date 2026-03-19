using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using Juice.Messaging.Context;
using Juice.Messaging.Publishing;
using Microsoft.Extensions.Logging;

namespace Juice.Messaging.Local.Internal
{
    internal class LocalChannelMessagePublisher : IMessagePublisher
    {
        public string Key => "local-channel";
        protected readonly ChannelWriter<ChannelEnvelope> _channelWriter;
        protected readonly ILogger _logger;

        public LocalChannelMessagePublisher(ChannelWriter<ChannelEnvelope> channelWriter, ILogger<LocalChannelMessagePublisher> logger)
        {
            _channelWriter = channelWriter;
            _logger = logger;
        }

        public async ValueTask PublishAsync(IMessage message, MessageContextData? context, CancellationToken cancellationToken = default)
        {
            await _channelWriter.WriteAsync(new ChannelEnvelope(message, context), cancellationToken);
        }
    }
}
