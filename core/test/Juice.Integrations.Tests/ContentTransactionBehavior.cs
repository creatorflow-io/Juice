using System;
using Juice.EF.Tests.Infrastructure;
using Juice.EventBus.Transactional;
using Juice.Integrations.MediatR.Behaviors;
using Juice.MediatR;
using Microsoft.Extensions.Logging;

namespace Juice.Integrations.Tests
{
    internal class ContentTransactionBehavior<TRequest, TResponse>
        : TransactionBehavior<TRequest, TResponse, TestContext>
        where TRequest : IRequest<TResponse>, IContentCommand
    {
        public ContentTransactionBehavior(TestContext dbContext,
            IIntegrationEventService<TestContext> integrationEventService,
            IMediator mediator,
            ILogger<ContentTransactionBehavior<TRequest, TResponse>> logger)
            : base(dbContext, integrationEventService, mediator, logger)
        {
        }
    }
}
