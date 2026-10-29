using System.Diagnostics;
using System.Reflection;

namespace Juice.Messaging.Diagnostics
{
    /// <summary>
    /// The one <see cref="ActivitySource"/> Juice records its spans on.
    /// <para>Juice does not reference the OpenTelemetry SDK: the host collects these spans by adding
    /// <see cref="Name"/> to its tracer provider (<c>AddSource(JuiceActivitySource.Name)</c>) and chooses the
    /// exporters and sampling. Without a listener no activity is created and the cost is a listener check.</para>
    /// </summary>
    public static class JuiceActivitySource
    {
        public const string Name = "Juice";

        public static readonly ActivitySource Source = new(Name, GetVersion());

        private static string? GetVersion()
        {
            var version = typeof(JuiceActivitySource).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            // drop the "+commit" build metadata
            var plus = version?.IndexOf('+') ?? -1;
            return plus > 0 ? version!.Substring(0, plus) : version;
        }
    }
}
