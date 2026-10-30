using Juice.MediatR;
using Juice.Messaging;
using Juice.Messaging.Policies;

namespace Juice.Samples.Observability
{
    // "orders" side: the HTTP endpoint sends PlaceOrder through the mediator, which publishes OrderPlaced.

    public sealed record PlaceOrder(string OrderNumber, bool FailBilling) : MessageBase, IRequest;

    public sealed class PlaceOrderHandler(IMessageService messages, ILogger<PlaceOrderHandler> logger) : IRequestHandler<PlaceOrder>
    {
        public async ValueTask Handle(PlaceOrder request, CancellationToken cancellationToken)
        {
            logger.LogInformation("Placing order {OrderNumber}", request.OrderNumber);
            await messages.PublishAsync(new OrderPlaced { OrderNumber = request.OrderNumber, FailBilling = request.FailBilling }, cancellationToken);
        }
    }

    public sealed record OrderPlaced : IntegrationEvent
    {
        public string OrderNumber { get; init; } = "";
        public bool FailBilling { get; init; }
    }

    // "billing" side: consumes OrderPlaced and sends its own mediator request, as a separate service would.

    public sealed class OrderPlacedHandler(IMediator mediator, ILogger<OrderPlacedHandler> logger) : IIntegrationEventHandler<OrderPlaced>
    {
        public async Task HandleAsync(OrderPlaced @event)
        {
            logger.LogInformation("Billing order {OrderNumber}", @event.OrderNumber);
            await mediator.Send(new CreateInvoice(@event.OrderNumber, @event.FailBilling));
        }
    }

    public sealed record CreateInvoice(string OrderNumber, bool Fail) : MessageBase, IRequest;

    public sealed class CreateInvoiceHandler(ILogger<CreateInvoiceHandler> logger) : IRequestHandler<CreateInvoice>
    {
        public ValueTask Handle(CreateInvoice request, CancellationToken cancellationToken)
        {
            if (request.Fail)
            {
                throw new InvalidOperationException($"No billing account for order {request.OrderNumber}");
            }
            logger.LogInformation("Invoice created for {OrderNumber}", request.OrderNumber);
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Every event goes to the in-process local-channel transport.
    /// </summary>
    internal sealed class LocalChannelPolicy : IMessagePublishingPolicy
    {
        public ValueTask<IReadOnlyCollection<PublishRoute>> ResolveAsync(PolicyResolveContext context)
        {
            IReadOnlyCollection<PublishRoute> routes = [new PublishRoute("local-channel", string.Empty)];
            return ValueTask.FromResult(routes);
        }
    }
}
