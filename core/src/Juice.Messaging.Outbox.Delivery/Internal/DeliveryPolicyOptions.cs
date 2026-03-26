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
        /// Interval between batch processing cycles
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
            );
        }
    }
}
