namespace Juice.Messaging.Outbox.Delivery.Internal
{
    public sealed class DeliveryPolicyOptions
    {
        /// <summary>
        /// Policies per Publisher × Intent × Context combination
        /// Key format: "{PublisherKey}:{Intent}:{Context}" or wildcards "*"
        /// </summary>
        public Dictionary<string, PolicyConfiguration> Policies { get; set; } = new();
        
        /// <summary>
        /// Default policy applied when no specific policy matches
        /// </summary>
        public PolicyConfiguration? DefaultPolicy { get; set; }
    }
    
    public sealed class PolicyConfiguration
    {
        /// <summary>
        /// How long to wait when no more deliveries are waiting. A full batch is followed by the next one at once.
        /// </summary>
        public TimeSpan? Interval { get; set; }
        
        /// <summary>
        /// Number of deliveries to process in each batch
        /// </summary>
        public int? BatchSize { get; set; }
        
        /// <summary>
        /// Timeout for in-progress deliveries before recovery
        /// </summary>
        public TimeSpan? Timeout { get; set; }
        
        /// <summary>
        /// Initial delay before first retry
        /// </summary>
        public TimeSpan? InitialRetryDelay { get; set; }
        
        /// <summary>
        /// Multiplier for exponential backoff (e.g., 2.0 = double each time)
        /// </summary>
        public double? RetryDelayMultiplier { get; set; }
        
        /// <summary>
        /// Maximum number of retry attempts before giving up
        /// </summary>
        public int? MaxRetryAttempts { get; set; }

        /// <summary>
        /// The longest wait between two attempts of a delivery
        /// </summary>
        public TimeSpan? MaxRetryDelay { get; set; }

        /// <summary>
        /// How many transactions' messages to send at the same time. 1 sends every message in order.
        /// </summary>
        public int? MaxConcurrency { get; set; }
        
        /// <summary>
        /// Converts this configuration to a DeliveryPolicy, merging with defaults
        /// </summary>
        public DeliveryPolicy ToPolicy(DeliveryPolicy? defaultPolicy = null)
        {
            var basePolicy = defaultPolicy ?? DeliveryPolicy.Default;
            
            return new DeliveryPolicy(
                Interval: Interval ?? basePolicy.Interval,
                BatchSize: BatchSize ?? basePolicy.BatchSize,
                Timeout: Timeout ?? basePolicy.Timeout,
                InitialRetryDelay: InitialRetryDelay ?? basePolicy.InitialRetryDelay,
                RetryDelayMultiplier: RetryDelayMultiplier ?? basePolicy.RetryDelayMultiplier,
                MaxRetryAttempts: MaxRetryAttempts ?? basePolicy.MaxRetryAttempts
            )
            {
                MaxConcurrency = MaxConcurrency ?? basePolicy.MaxConcurrency,
                MaxRetryDelay = MaxRetryDelay ?? basePolicy.MaxRetryDelay
            };
        }
    }
}
