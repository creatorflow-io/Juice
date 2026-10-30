namespace Juice.OpenTelemetry
{
    /// <summary>
    /// What the Juice log processor (<c>AddJuiceInstrumentation()</c> on the logging builder) exports. Exporters,
    /// endpoints, log levels and <c>IncludeScopes</c> / <c>IncludeFormattedMessage</c> belong to the host's
    /// OpenTelemetry configuration and are not repeated here.
    /// </summary>
    public class JuiceLogOptions
    {
        /// <summary>
        /// Category prefix of the logs written by Juice. Payload redaction applies to these categories only: the
        /// application owns what its own logs contain. Default <c>"Juice."</c>.
        /// </summary>
        public string CategoryPrefix { get; set; } = "Juice.";

        /// <summary>
        /// Exports the message payloads and destructured objects (<c>{@Command}</c>, <c>{Payload}</c>) some Juice
        /// debug and error logs carry. Default <c>false</c>: a payload can hold personal data or secrets.
        /// </summary>
        public bool IncludePayloads { get; set; }

        /// <summary>
        /// Attribute names removed from Juice logs while <see cref="IncludePayloads"/> is <c>false</c>, in addition to
        /// every destructured (<c>@</c>-prefixed) attribute.
        /// </summary>
        public ISet<string> PayloadAttributes { get; } = new HashSet<string>(StringComparer.Ordinal) { "Payload" };
    }
}
