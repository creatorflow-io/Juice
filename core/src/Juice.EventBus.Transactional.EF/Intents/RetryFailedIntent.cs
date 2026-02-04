using Juice.EventBus.Delivery;
using Juice.EventBus.Delivery.Policies;
using Microsoft.EntityFrameworkCore;

namespace Juice.EventBus.Transactional.EF.Intents
{
    internal class RetryFailedIntent<TContext> : IOutboxIntent<TContext>
        where TContext : class, IOutboxContext
    {
        public string Name => "retry-failed";
        private readonly TContext _context;
        public RetryFailedIntent(TContext context)
        {
            _context = context;
        }
        public async Task<IEnumerable<OutboxDelivery>> RetrieveDeliveriesAsync(string publisherKey, DeliveryPolicy policy, CancellationToken cancellationToken)
        {
            return await _context.OutboxDeliveries
                .AsNoTracking()
                .Include(d => d.OutboxEvent)
                .Where(d => d.State == DeliveryState.Failed && d.NextAttemptOn != null && d.NextAttemptOn < DateTimeOffset.Now)
                .Where(d => d.PublisherKey == publisherKey)
                .OrderBy(d => d.NextAttemptOn)
                .Take(policy.BatchSize)
                .ToListAsync(cancellationToken);
        }
    }
}
