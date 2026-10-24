using NATS.Client.JetStream.Models;

namespace Juice.EventBus.NATS.Infrastructure
{
    public enum JetStreamStorage
    {
        File,
        Memory
    }

    public enum JetStreamRetention
    {
        Limits,
        Interest,
        /// <summary>
        /// Each message is delivered to one consumer only; multiple consumers with overlapping filters are rejected by the server.
        /// </summary>
        WorkQueue
    }

    /// <summary>
    /// A stream declared by <c>JetStreamInfrastructureBuilder</c>. Existing streams are never updated.
    /// </summary>
    public sealed class StreamDefinition
    {
        public string Name { get; set; } = default!;
        public string[] Subjects { get; set; } = [];
        public JetStreamStorage Storage { get; set; } = JetStreamStorage.File;
        public JetStreamRetention Retention { get; set; } = JetStreamRetention.Limits;
        /// <summary>
        /// Maximum age of messages; null for unlimited.
        /// </summary>
        public TimeSpan? MaxAge { get; set; }
        public long? MaxBytes { get; set; }
        public long? MaxMsgs { get; set; }
        public int? MaxMsgSize { get; set; }
        /// <summary>
        /// Window in which messages with the same id are stored once. Default is 10 minutes, longer than
        /// the default outbox recover-timeout (5 minutes) plus its interval; keep it longer than the
        /// <c>Timeout</c> of your outbox delivery policy so a recovered republish is still de-duplicated.
        /// </summary>
        public TimeSpan DuplicateWindow { get; set; } = TimeSpan.FromMinutes(10);
        /// <summary>
        /// Number of replicas, 1..5. Default is 1.
        /// </summary>
        public int Replicas { get; set; } = 1;

        internal void Validate()
        {
            JetStreamNaming.ValidateToken(Name, nameof(Name));
            if (Name.Contains('.'))
            {
                throw new ArgumentException($"Stream name '{Name}' must not contain '.'.", nameof(Name));
            }
            if (Subjects == null || Subjects.Length == 0)
            {
                throw new ArgumentException($"Stream '{Name}' must capture at least one subject.", nameof(Subjects));
            }
            if (Replicas < 1 || Replicas > 5)
            {
                throw new ArgumentOutOfRangeException(nameof(Replicas), $"Stream '{Name}' replicas must be between 1 and 5.");
            }
        }

        internal StreamConfig ToStreamConfig()
        {
            var config = new StreamConfig(Name, Subjects.ToList())
            {
                Storage = Storage == JetStreamStorage.Memory ? StreamConfigStorage.Memory : StreamConfigStorage.File,
                Retention = Retention switch
                {
                    JetStreamRetention.Interest => StreamConfigRetention.Interest,
                    JetStreamRetention.WorkQueue => StreamConfigRetention.Workqueue,
                    _ => StreamConfigRetention.Limits
                },
                DuplicateWindow = DuplicateWindow,
                NumReplicas = Replicas
            };
            if (MaxAge.HasValue)
            {
                config.MaxAge = MaxAge.Value;
            }
            if (MaxBytes.HasValue)
            {
                config.MaxBytes = MaxBytes.Value;
            }
            if (MaxMsgs.HasValue)
            {
                config.MaxMsgs = MaxMsgs.Value;
            }
            if (MaxMsgSize.HasValue)
            {
                config.MaxMsgSize = MaxMsgSize.Value;
            }
            return config;
        }
    }

    /// <summary>
    /// How streams and consumers are handled at startup.
    /// </summary>
    public enum JetStreamProvisioning
    {
        /// <summary>
        /// Fail when a declared stream or consumer does not exist. Never changes server topology.
        /// </summary>
        VerifyOnly,
        /// <summary>
        /// Create declared streams and consumers that do not exist. Existing ones are never updated.
        /// </summary>
        CreateIfMissing
    }

    /// <summary>
    /// What a consumer does with a message that no handler is subscribed to.
    /// </summary>
    public enum NotHandledBehavior
    {
        Terminate,
        Park
    }
}
