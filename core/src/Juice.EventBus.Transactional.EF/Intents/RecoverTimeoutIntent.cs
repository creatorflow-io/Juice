using Juice.EventBus.Delivery;
using Juice.EventBus.Delivery.Policies;
using Microsoft.EntityFrameworkCore;

namespace Juice.EventBus.Transactional.EF.Intents
{
    internal class RecoverTimeoutIntent<TContext>: IOutboxIntent<TContext>
        where TContext : class, IOutboxContext
    {
        public string Name => "recover-timeout";
        private readonly TContext _context;
        public RecoverTimeoutIntent(TContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<OutboxDelivery>> RetrieveDeliveriesAsync(string publisherKey, DeliveryPolicy policy, CancellationToken cancellationToken)
        {
            var timeoutAt = DateTimeOffset.UtcNow.Add(policy.Timeout);
            return await _context.OutboxDeliveries
                .AsNoTracking()
                .Include(d => d.OutboxEvent)
                .Where(d => d.State == DeliveryState.InProgress && d.ProcessedOn < timeoutAt)
                .Where(d => d.PublisherKey == publisherKey)
                .OrderBy(d => d.ProcessedOn)
                .Take(policy.BatchSize)
                .ToListAsync(cancellationToken);
        }
    }
}
