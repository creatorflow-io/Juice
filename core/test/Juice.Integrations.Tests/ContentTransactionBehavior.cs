using Juice.EF.Tests.Infrastructure;
using Juice.Integrations.EventBus;
using Juice.Integrations.MediatR.Behaviors;
using Juice.MediatR;
using Microsoft.Extensions.Logging;

namespace Juice.Integrations.Tests
{
    internal class ContentTransactionBehavior : TransactionBehavior<CreateContentCommand, IOperationResult, TestContext>
    {
        public ContentTransactionBehavior(TestContext dbContext,
            IIntegrationEventService<TestContext> integrationEventService,
            IMediator mediator,
            ILogger<ContentTransactionBehavior> logger) : base(dbContext, integrationEventService, mediator, logger)
        {
        }
    }
}
