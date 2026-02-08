using System;
using Juice.EF.Tests.Infrastructure;
using Juice.MediatR;
using Juice.MediatR.Behaviors;
using Juice.Messaging.Outbox;
using Microsoft.Extensions.Logging;

namespace Juice.Integrations.Tests
{
    internal class ContentTransactionBehavior<TRequest, TResponse>
        : TransactionBehavior<TRequest, TResponse, TestContext>
        where TRequest : IRequest<TResponse>, IContentCommand
    {
        public ContentTransactionBehavior(TestContext dbContext,
            IOutboxService<TestContext> integrationEventService,
            IMediator mediator,
            ILogger<ContentTransactionBehavior<TRequest, TResponse>> logger)
            : base(dbContext, integrationEventService, mediator, logger)
        {
        }
    }
}
