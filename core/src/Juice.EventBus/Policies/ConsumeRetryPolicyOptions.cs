namespace Juice.EventBus.Policies
{

    public class ConsumeRetryPolicyDefinition
    {
        /// <summary>
        /// Source identifier (exchange/topic/queue)
        /// </summary>
        public string Source { get; set; } = default!;
        /// <summary>
        ///  Dead-letter/parking destination identifier (exchange/topic/queue)
        /// </summary>
        public string DeadLetterDest { get; set; } = default!;
        
        /// <summary>
        /// Retry delay tiers (e.g., ["10s", "1m", "5m", "15m"])
        /// </summary>
        public string[] RetryTiers { get; set; } = [];
        /// <summary>
        /// Defines the maximum number of retry attempts. If not set, all tiers will be used.
        /// </summary>
        public int? MaxRetryAttempts { get; set; }
        /// <summary>
        /// Determines whether parking is enabled after all retry attempts are exhausted.
        /// </summary>
        public bool IsParkingEnabled { get; set; }
    }

    /// <summary>
    /// The options for configuring consume retry policies. Used to define how message processing retries are handled for different sources.
    /// </summary>
    public class ConsumeRetryPolicyOptions
    {
        public ConsumeRetryPolicyDefinition? Default { get; set; }
        public ConsumeRetryPolicyDefinition[] Policies { get; set; } = [];

        public void AddPolicy(ConsumeRetryPolicyDefinition policy)
        {
            var policiesList = Policies.ToList();
            policiesList.Add(policy);
            Policies = policiesList.ToArray();
        }

        public void ClearPolicies()
        {
            Policies = [];
        }
    }
}
