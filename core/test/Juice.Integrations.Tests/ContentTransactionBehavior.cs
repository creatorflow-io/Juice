using System;
using Juice.EF.Tests.Infrastructure;
using Juice.MediatR;
using Juice.MediatR.Behaviors;
using Juice.Messaging.Outbox;
using Microsoft.Extensions.Logging;

namespace Juice.Integrations.Tests
{
    internal class ContentTransactionBehavior<TRequest, TResponse>
        : TransactionBehavior<TRequest, TResponse, Juice.EF.Tests.Infrastructure.TestContext>
        where TRequest : IRequest<TResponse>, IContentCommand
    {
        public ContentTransactionBehavior(Juice.EF.Tests.Infrastructure.TestContext dbContext,
            IOutboxService<Juice.EF.Tests.Infrastructure.TestContext> integrationEventService,
            IMediator mediator,
            ILogger<ContentTransactionBehavior<TRequest, TResponse>> logger)
            : base(dbContext, integrationEventService, mediator, logger)
        {
        }
    }
}
