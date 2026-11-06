using Juice.Messaging.Outbox.Delivery;
using Microsoft.EntityFrameworkCore;

namespace Juice.Messaging.Outbox.EF.Intents
{
    internal class SendPendingIntent<TContext> : IDeliveryIntent<TContext>
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
            // NextAttemptOn is computed with the app clock, so compare with it rather than the database clock
            var now = DateTimeOffset.UtcNow;
            return await _context.OutboxDeliveries
                .AsNoTracking()
                .Include(d => d.OutboxEvent)
                .Where(d => d.State == DeliveryState.NotPublished)
                .Where(d => d.PublisherKey == publisherKey)
                .Where(d => d.NextAttemptOn == null || d.NextAttemptOn < now) // only a deferred delivery has one here
                .OrderBy(d => d.CreationTime)
                .Take(policy.BatchSize)
                .ToListAsync(cancellationToken);
        }
    }
}
