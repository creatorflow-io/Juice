using Juice.MediatR;
using Microsoft.Extensions.Options;

namespace Juice.OpenTelemetry.Mediator
{
    /// <summary>
    /// Outermost behavior (<see cref="int.MinValue"/>): the span and duration cover every other behavior, idempotency
    /// and the transaction included.
    /// </summary>
    internal sealed class MediatorActivityBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    {
        private readonly IOptions<JuiceOpenTelemetryOptions> _options;

        public MediatorActivityBehavior(IOptions<JuiceOpenTelemetryOptions> options)
        {
            _options = options;
        }

        public int Order => int.MinValue;

        public ValueTask<TResponse> Handle(TRequest request, RequestHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken)
            => _options.Value.ShouldInstrument(typeof(TRequest), notification: false)
                ? MediatorTelemetry.RunAsync(typeof(TRequest), "request", () => next.Invoke(request, cancellationToken))
                : next.Invoke(request, cancellationToken);
    }

    /// <inheritdoc cref="MediatorActivityBehavior{TRequest, TResponse}"/>
    internal sealed class MediatorActivityBehavior<TRequest> : IPipelineBehavior<TRequest>
        where TRequest : IRequest
    {
        private readonly IOptions<JuiceOpenTelemetryOptions> _options;

        public MediatorActivityBehavior(IOptions<JuiceOpenTelemetryOptions> options)
        {
            _options = options;
        }

        public int Order => int.MinValue;

        public ValueTask Handle(TRequest request, RequestHandlerDelegate<TRequest> next, CancellationToken cancellationToken)
            => _options.Value.ShouldInstrument(typeof(TRequest), notification: false)
                ? MediatorTelemetry.RunAsync(typeof(TRequest), "request", () => next.Invoke(request, cancellationToken))
                : next.Invoke(request, cancellationToken);
    }

    /// <inheritdoc cref="MediatorActivityBehavior{TRequest, TResponse}"/>
    internal sealed class MediatorNotificationActivityBehavior<TNotification> : INotificationPipelineBehavior<TNotification>
        where TNotification : INotification
    {
        private readonly IOptions<JuiceOpenTelemetryOptions> _options;

        public MediatorNotificationActivityBehavior(IOptions<JuiceOpenTelemetryOptions> options)
        {
            _options = options;
        }

        public int Order => int.MinValue;

        public ValueTask Handle(TNotification notification, NotificationHandlerDelegate<TNotification> next, CancellationToken cancellationToken)
            => _options.Value.ShouldInstrument(typeof(TNotification), notification: true)
                ? MediatorTelemetry.RunAsync(typeof(TNotification), "notification", () => next.Invoke(notification, cancellationToken))
                : next.Invoke(notification, cancellationToken);
    }

    internal static class JuiceOpenTelemetryOptionsExtensions
    {
        public static bool ShouldInstrument(this JuiceOpenTelemetryOptions options, Type type, bool notification)
            => options.EnableMediatorInstrumentation
                && (!notification || options.EnableNotificationInstrumentation)
                && (options.Filter?.Invoke(type) ?? true);
    }
}
