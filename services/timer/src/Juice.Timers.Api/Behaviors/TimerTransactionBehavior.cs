using Juice.Integrations.EventBus;
using Juice.Integrations.MediatR.Behaviors;
using Juice.Timers.EF;

namespace Juice.Timers.Api.Behaviors
{
    internal class TimerTransactionBehavior<T, R> : TransactionBehavior<T, R, TimerDbContext>
        where T : IRequest<R>
    {
        public TimerTransactionBehavior(TimerDbContext dbContext, IIntegrationEventService<TimerDbContext> integrationEventService, ILogger<TimerTransactionBehavior<T, R>> logger) : base(dbContext, integrationEventService, logger)
        {
        }
    }
}
