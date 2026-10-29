using System.Diagnostics;
using System.Text;
using Juice.Messaging.Integrations;

namespace Juice.Messaging.Diagnostics
{
    /// <summary>
    /// Starts and tags the messaging spans of <see cref="JuiceActivitySource"/> and carries the W3C trace context
    /// (<c>traceparent</c>, <c>tracestate</c>) in message headers.
    /// <para>The trace context is separate from <see cref="MessageContext"/>: the correlation, causation and execution
    /// ids keep their own <c>x-*</c> headers and are only copied onto spans as <c>juice.*</c> tags.</para>
    /// <para>Every method accepts a null activity and never throws because of telemetry.</para>
    /// </summary>
    public static class MessagingActivity
    {
        public const string TraceParentHeader = "traceparent";
        public const string TraceStateHeader = "tracestate";

        /// <summary>
        /// Producer span for a message written to the outbox. Its context is stored with the message, so every
        /// delivery and consumer of the message continues this trace.
        /// </summary>
        public static Activity? StartCreate(IMessage message)
        {
            if (!JuiceActivitySource.Source.HasListeners())
            {
                return null;
            }
            var messageType = message.GetType().Name;
            var activity = JuiceActivitySource.Source.StartActivity("create " + messageType, ActivityKind.Producer);
            if (activity is { IsAllDataRequested: true })
            {
                SetMessagingTags(activity, "create", system: null, destination: null, messageType, message.MessageId.ToString());
                EnrichWithMessageContext(activity);
            }
            return activity;
        }

        /// <summary>
        /// Producer span for handing a message to a transport.
        /// </summary>
        public static Activity? StartSend(string system, string? destination, string? messageType, string? messageId)
        {
            if (!JuiceActivitySource.Source.HasListeners())
            {
                return null;
            }
            var activity = JuiceActivitySource.Source.StartActivity("send " + (destination ?? messageType ?? system), ActivityKind.Producer);
            if (activity is { IsAllDataRequested: true })
            {
                SetMessagingTags(activity, "send", system, destination, messageType, messageId);
                EnrichWithMessageContext(activity);
            }
            return activity;
        }

        /// <summary>
        /// Span for one outbox delivery attempt, continuing the trace the message was created in.
        /// </summary>
        public static Activity? StartDelivery(string publisherKey, string? messageType, string? messageId, IDictionary<string, object?>? headers)
        {
            if (!JuiceActivitySource.Source.HasListeners())
            {
                return null;
            }
            var activity = JuiceActivitySource.Source.StartActivity("deliver " + publisherKey, ActivityKind.Internal, Extract(headers));
            if (activity is { IsAllDataRequested: true })
            {
                activity.SetTag(Tags.Component, "outbox");
                activity.SetTag(Tags.Publisher, publisherKey);
                activity.SetTag(Tags.MessageType, messageType);
                activity.SetTag(Tags.MessageId, messageId);
            }
            return activity;
        }

        /// <summary>
        /// Consumer span for a received message. The parent is the trace context in <paramref name="headers"/>;
        /// when the current activity already belongs to that trace (in-process delivery) the span nests under it.
        /// Call <see cref="EnrichWithMessageContext"/> once <see cref="MessageContext"/> is initialized for the message.
        /// </summary>
        public static Activity? StartProcess(string system, string? destination, string? messageType, string? messageId,
            IDictionary<string, object?>? headers, string? consumerGroup = null)
            => StartProcess(system, destination, messageType, messageId, Extract(headers), consumerGroup);

        /// <inheritdoc cref="StartProcess(string, string?, string?, string?, IDictionary{string, object?}?, string?)"/>
        public static Activity? StartProcess(string system, string? destination, string? messageType, string? messageId,
            ActivityContext parent, string? consumerGroup = null)
        {
            if (!JuiceActivitySource.Source.HasListeners())
            {
                return null;
            }
            var current = Activity.Current;
            if (current != null && (parent == default || current.TraceId == parent.TraceId))
            {
                parent = current.Context;
            }
            var activity = JuiceActivitySource.Source.StartActivity("process " + (destination ?? messageType ?? system), ActivityKind.Consumer, parent);
            if (activity is { IsAllDataRequested: true })
            {
                SetMessagingTags(activity, "process", system, destination, messageType, messageId);
                activity.SetTag("messaging.consumer.group.name", consumerGroup);
            }
            return activity;
        }

        /// <summary>
        /// Span for one integration event handler.
        /// </summary>
        public static Activity? StartHandle(Type handlerType, string? messageType)
        {
            if (!JuiceActivitySource.Source.HasListeners())
            {
                return null;
            }
            var activity = JuiceActivitySource.Source.StartActivity("handle " + handlerType.Name, ActivityKind.Internal);
            if (activity is { IsAllDataRequested: true })
            {
                activity.SetTag(Tags.Component, "messaging");
                activity.SetTag(Tags.Handler, handlerType.FullName);
                activity.SetTag(Tags.MessageType, messageType);
                EnrichWithMessageContext(activity);
            }
            return activity;
        }

        /// <summary>
        /// Writes the trace context of <paramref name="activity"/> (or <see cref="Activity.Current"/>) into
        /// <paramref name="headers"/> through <see cref="DistributedContextPropagator.Current"/>.
        /// A header that already holds a trace context is kept: a message carries the context it was created in.
        /// Only <c>traceparent</c> and <c>tracestate</c> are written, baggage is not copied into messages.
        /// </summary>
        public static void Inject(IDictionary<string, object?> headers, Activity? activity = null)
        {
            activity ??= Activity.Current;
            if (activity == null || headers.ContainsKey(TraceParentHeader))
            {
                return;
            }
            try
            {
                DistributedContextPropagator.Current.Inject(activity, headers, static (carrier, key, value) =>
                {
                    if (carrier is IDictionary<string, object?> dictionary
                        && (key == TraceParentHeader || key == TraceStateHeader))
                    {
                        dictionary[key] = value;
                    }
                });
            }
            catch
            {
                // telemetry must not fail the publish
            }
        }

        /// <summary>
        /// Reads the trace context from message headers; <c>default</c> when there is none or it is invalid.
        /// Values may be strings or UTF-8 bytes (AMQP headers).
        /// </summary>
        public static ActivityContext Extract(IDictionary<string, object?>? headers)
        {
            if (headers == null || !headers.ContainsKey(TraceParentHeader))
            {
                return default;
            }
            try
            {
                DistributedContextPropagator.Current.ExtractTraceIdAndState(headers,
                    static (object? carrier, string name, out string? value, out IEnumerable<string>? values) =>
                    {
                        values = null;
                        value = carrier is IDictionary<string, object?> dictionary && dictionary.TryGetValue(name, out var raw)
                            ? raw switch
                            {
                                null => null,
                                string s => s,
                                byte[] bytes => Encoding.UTF8.GetString(bytes),
                                _ => raw.ToString()
                            }
                            : null;
                    },
                    out var traceParent, out var traceState);
#if NET7_0_OR_GREATER
                return traceParent != null && ActivityContext.TryParse(traceParent, traceState, isRemote: true, out var context)
#else
                return traceParent != null && ActivityContext.TryParse(traceParent, traceState, out var context)
#endif
                    ? context
                    : default;
            }
            catch
            {
                return default;
            }
        }

        /// <summary>
        /// Copies the <see cref="MessageContext"/> ids onto the activity as <c>juice.*</c> tags.
        /// </summary>
        public static void EnrichWithMessageContext(Activity? activity)
        {
            if (activity is not { IsAllDataRequested: true } || !MessageContext.IsInitialized)
            {
                return;
            }
            var context = MessageContext.Current;
            activity.SetTag(Tags.CorrelationId, context.CorrelationId);
            activity.SetTag(Tags.CausationId, context.CausationId);
            activity.SetTag(Tags.ExecutionId, context.ExecutionId);
            activity.SetTag(Tags.Source, context.Source);
        }

        /// <summary>
        /// Records the dispatch result; <see cref="EventDispatchResult.Failure"/> marks the span as failed.
        /// </summary>
        public static void SetResult(Activity? activity, EventDispatchResult result)
        {
            if (activity == null)
            {
                return;
            }
            activity.SetTag(Tags.DispatchResult, result switch
            {
                EventDispatchResult.Success => "success",
                EventDispatchResult.Failure => "failure",
                EventDispatchResult.Duplicated => "duplicated",
                EventDispatchResult.NotHandled => "not_handled",
                _ => result.ToString()
            });
            if (result == EventDispatchResult.Failure)
            {
                activity.SetStatus(ActivityStatusCode.Error, "One or more handlers failed");
            }
        }

        /// <summary>
        /// Marks the span as failed and records the exception as an <c>exception</c> event.
        /// </summary>
        public static void SetError(Activity? activity, Exception exception)
        {
            if (activity == null)
            {
                return;
            }
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            if (activity.IsAllDataRequested)
            {
                activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
                {
                    { "exception.type", exception.GetType().FullName },
                    { "exception.message", exception.Message },
                    { "exception.stacktrace", exception.ToString() }
                }));
            }
        }

        private static void SetMessagingTags(Activity activity, string operation, string? system, string? destination,
            string? messageType, string? messageId)
        {
            activity.SetTag(Tags.Component, "messaging");
            activity.SetTag("messaging.operation.type", operation);
            activity.SetTag("messaging.operation.name", operation);
            activity.SetTag("messaging.system", system);
            activity.SetTag("messaging.destination.name", destination);
            activity.SetTag(Tags.MessageId, messageId);
            activity.SetTag(Tags.MessageType, messageType);
        }

        /// <summary>
        /// Tag names recorded on Juice spans.
        /// </summary>
        public static class Tags
        {
            public const string Component = "juice.component";
            public const string MessageType = "juice.message_type";
            public const string MessageId = "messaging.message.id";
            public const string Publisher = "juice.publisher";
            public const string Handler = "juice.handler";
            public const string DispatchResult = "juice.dispatch.result";
            public const string DeliveryOutcome = "juice.delivery.outcome";
            public const string RetryCount = "juice.delivery.retry_count";
            public const string CorrelationId = "juice.correlation_id";
            public const string CausationId = "juice.causation_id";
            public const string ExecutionId = "juice.execution_id";
            public const string Source = "juice.source";
        }
    }
}
