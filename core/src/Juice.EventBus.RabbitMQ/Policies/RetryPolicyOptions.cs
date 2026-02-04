namespace Juice.EventBus.RabbitMQ.Policies
{

    internal class RetryPolicyDefinition
    {
        public string SourceExchange { get; set; } = default!;
        public string RetryExchange { get; set; } = default!;
        public string[] RetryTiers { get; set; } = [];
    }
    internal class RetryPolicyOptions
    {
        public RetryPolicyDefinition? Default { get; set; }
        public RetryPolicyDefinition[] Policies { get; set; } = [];
    }
}
