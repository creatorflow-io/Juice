using Juice.Messaging;
using Juice.Messaging.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Juice.OpenTelemetry.Logging
{
    /// <summary>
    /// Runs on every log record before the exporters: adds the <see cref="MessageContext"/> ids with the span tag names
    /// and removes payloads from Juice logs. The SDK already sets the trace and span ids from <c>Activity.Current</c>.
    /// </summary>
    internal sealed class JuiceLogProcessor : BaseProcessor<LogRecord>
    {
        private readonly JuiceLogOptions _options;

        public JuiceLogProcessor(JuiceLogOptions options)
        {
            _options = options;
        }

        public override void OnEnd(LogRecord data)
        {
            var redact = !_options.IncludePayloads
                && data.CategoryName != null
                && data.CategoryName.StartsWith(_options.CategoryPrefix, StringComparison.Ordinal);
            var addContext = MessageContext.IsInitialized;
            if (!redact && !addContext)
            {
                return;
            }

            var attributes = new List<KeyValuePair<string, object?>>((data.Attributes?.Count ?? 0) + 4);
            var redacted = false;
            if (data.Attributes != null)
            {
                foreach (var attribute in data.Attributes)
                {
                    if (redact && IsPayload(attribute.Key))
                    {
                        redacted = true;
                        continue;
                    }
                    attributes.Add(attribute);
                }
            }
            if (redacted)
            {
                // formatted with the payload; the body keeps the message template
                data.FormattedMessage = null;
            }
            if (addContext)
            {
                var context = MessageContext.Current;
                attributes.Add(new(MessagingActivity.Tags.CorrelationId, context.CorrelationId));
                attributes.Add(new(MessagingActivity.Tags.CausationId, context.CausationId));
                attributes.Add(new(MessagingActivity.Tags.ExecutionId, context.ExecutionId));
                attributes.Add(new(MessagingActivity.Tags.Source, context.Source));
            }
            data.Attributes = attributes;
        }

        private bool IsPayload(string key)
            => key.StartsWith('@') || _options.PayloadAttributes.Contains(key);
    }
}
