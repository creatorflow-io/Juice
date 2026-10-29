namespace Juice.OpenTelemetry
{
    /// <summary>
    /// Juice-specific instrumentation switches. Exporters, endpoints and sampling belong to the host's OpenTelemetry
    /// configuration and are not repeated here.
    /// <para>Messaging, outbox and HTTP context instrumentation is part of the Juice core packages: it is collected when
    /// the host adds the Juice source (<c>AddJuiceInstrumentation()</c>) and is not switched here.</para>
    /// </summary>
    public class JuiceOpenTelemetryOptions
    {
        /// <summary>
        /// Records a span and the <c>juice.mediator.*</c> metrics for every mediator request. Default <c>true</c>.
        /// </summary>
        public bool EnableMediatorInstrumentation { get; set; } = true;

        /// <summary>
        /// Also records mediator notifications (<c>IMediator.Publish</c>). Default <c>true</c>.
        /// </summary>
        public bool EnableNotificationInstrumentation { get; set; } = true;

        /// <summary>
        /// Optional filter on the request or notification type; return <c>false</c> to skip instrumenting it
        /// (for example a high-frequency query).
        /// </summary>
        public Func<Type, bool>? Filter { get; set; }
    }
}
