using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.EventBus.NATS.Infrastructure
{
    public sealed class JetStreamInfrastructureBuilder
    {
        private readonly List<StreamDefinition> _streams = [];
        private readonly string _connectionName;
        private JetStreamProvisioning _provisioning = JetStreamProvisioning.VerifyOnly;

        public JetStreamInfrastructureBuilder(string connectionName)
        {
            _connectionName = connectionName;
        }

        internal IReadOnlyList<StreamDefinition> Streams => _streams;

        /// <summary>
        /// <see cref="JetStreamProvisioning.VerifyOnly"/> (default) fails when a declared stream is missing;
        /// <see cref="JetStreamProvisioning.CreateIfMissing"/> creates it. Existing streams are never changed.
        /// </summary>
        public JetStreamInfrastructureBuilder Provisioning(JetStreamProvisioning mode)
        {
            _provisioning = mode;
            return this;
        }

        /// <summary>
        /// Declare the stream that captures <c>{destination}.&gt;</c>, named after the destination
        /// (<c>x.content</c> → <c>X_CONTENT</c>).
        /// </summary>
        public JetStreamInfrastructureBuilder DeclareStream(string destination, Action<StreamDefinition>? configure = default)
        {
            JetStreamNaming.ValidateToken(destination, nameof(destination));
            return Declare(new StreamDefinition
            {
                Name = JetStreamNaming.DeriveStreamName(destination),
                Subjects = [$"{destination}.>"]
            }, configure);
        }

        /// <summary>
        /// Declare the parking stream for <paramref name="destination"/>: <c>{STREAM}_PARKING</c> capturing
        /// <c>parking.{destination}.&gt;</c>, kept for 14 days.
        /// </summary>
        public JetStreamInfrastructureBuilder DeclareParkingStream(string destination, Action<StreamDefinition>? configure = default)
        {
            JetStreamNaming.ValidateToken(destination, nameof(destination));
            return Declare(new StreamDefinition
            {
                Name = JetStreamNaming.ParkingStreamName(JetStreamNaming.DeriveStreamName(destination)),
                Subjects = [$"{JetStreamNaming.DefaultParkingPrefix(destination)}.>"],
                Retention = JetStreamRetention.Limits,
                MaxAge = TimeSpan.FromDays(14)
            }, configure);
        }

        private JetStreamInfrastructureBuilder Declare(StreamDefinition definition, Action<StreamDefinition>? configure)
        {
            configure?.Invoke(definition);
            definition.Validate();

            if (_streams.Any(s => s.Name == definition.Name))
            {
                throw new InvalidOperationException($"Stream '{definition.Name}' has already been declared.");
            }
            foreach (var other in _streams)
            {
                foreach (var subject in definition.Subjects)
                {
                    var overlap = other.Subjects.FirstOrDefault(s => SubjectsOverlap(s, subject));
                    if (overlap != null)
                    {
                        throw new InvalidOperationException(
                            $"Stream '{definition.Name}' subject '{subject}' overlaps '{overlap}' of stream '{other.Name}'.");
                    }
                }
            }
            _streams.Add(definition);
            return this;
        }

        /// <summary>
        /// True when some subject could match both filters (<c>*</c> matches one token, <c>&gt;</c> the rest).
        /// </summary>
        internal static bool SubjectsOverlap(string a, string b)
        {
            var ta = a.Split('.');
            var tb = b.Split('.');
            for (var i = 0; ; i++)
            {
                var endA = i >= ta.Length;
                var endB = i >= tb.Length;
                if (endA || endB)
                {
                    return endA && endB;
                }
                if (ta[i] == ">" || tb[i] == ">")
                {
                    return true;
                }
                if (ta[i] != "*" && tb[i] != "*" && ta[i] != tb[i])
                {
                    return false;
                }
            }
        }

        internal JetStreamInfrastructureInitializer Build(IServiceProvider sp)
        {
            var connection = sp.GetKeyedService<IJetStreamConnection>(_connectionName)
                ?? throw new InvalidOperationException($"JetStream connection with name '{_connectionName}' is not registered.");
            var logger = sp.GetRequiredService<ILogger<JetStreamInfrastructureInitializer>>();
            foreach (var stream in _streams.Where(s => s.Retention == JetStreamRetention.WorkQueue))
            {
                logger.LogWarning("Stream {Stream} uses WorkQueue retention: consumers with overlapping filters will be rejected by the server.", stream.Name);
            }
            return new JetStreamInfrastructureInitializer(connection, _streams.ToArray(), _provisioning, logger);
        }
    }
}
