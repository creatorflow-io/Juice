namespace Juice.Messaging.Outbox.Delivery.Internal
{
    internal class DefaultDeliveryPolicyResolver : IDeliveryPolicyResolver
    {
        private readonly IEnumerable<IDeliveryPolicyProvider> _policyProviders;
        
        public DefaultDeliveryPolicyResolver(IEnumerable<IDeliveryPolicyProvider> policyProviders)
        {
            _policyProviders = policyProviders;
        }
        
        public async ValueTask<DeliveryPolicy> GetPolicyAsync(DeliveryContext context, CancellationToken cancellationToken)
        {
            // Iterate through all registered policy providers
            // Allow providers to override/customize policies based on context
            foreach (var provider in _policyProviders)
            {
                var policy = await provider.GetPolicyAsync(context, cancellationToken);
                if (policy != null)
                {
                    return policy;
                }
            }
            
            // Fallback to default policy if no provider returns a policy
            return DeliveryPolicy.Default;
        }
    }
}
