using Juice.Integrations.EventBus;
using Juice.Integrations.MediatR.Behaviors;
using Juice.Workflows.Domain.Commands;
using Juice.Workflows.EF;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Juice.Workflows.Api.Behaviors
{
    internal class WorkflowStateTransactionBehavior<T, R> : TransactionBehavior<T, R, WorkflowPersistDbContext>
        where T : IRequest<R>, IWorkflowCommand
    {
        public WorkflowStateTransactionBehavior(WorkflowPersistDbContext dbContext, IIntegrationEventService<WorkflowPersistDbContext> integrationEventService,
            ILogger<WorkflowStateTransactionBehavior<T, R>> logger) : base(dbContext, integrationEventService, logger)
        {
        }
    }
}
