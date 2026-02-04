
using Juice.EventBus.Delivery;
using Juice.EventBus.Delivery.Policies;
using Microsoft.EntityFrameworkCore;

namespace Juice.EventBus.Transactional.EF.Intents
{
    internal class SendPendingIntent<TContext> : IOutboxIntent<TContext>
        where TContext : class, IOutboxContext
    {
        public string Name => "send-pending";
        private readonly TContext _context;
        public SendPendingIntent(TContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<OutboxDelivery>> RetrieveDeliveriesAsync(string publisherKey, DeliveryPolicy policy, CancellationToken cancellationToken)
        {
            return await _context.OutboxDeliveries
                .AsNoTracking()
                .Include(d => d.OutboxEvent)
                .Where(d => d.State == DeliveryState.NotPublished)
                .Where(d => d.PublisherKey == publisherKey)
                .OrderBy(d => d.CreationTime)
                .Take(policy.BatchSize)
                .ToListAsync(cancellationToken);
        }
    }
}
