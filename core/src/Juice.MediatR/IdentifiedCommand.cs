namespace Juice.MediatR
{
    public class IdentifiedCommand<TRequest> : IRequest, IIdentifiedRequest<TRequest>
        where TRequest : IRequest
    {
        public TRequest Command { get; }
        public Guid Id { get; }
        /// <summary>
        /// Deduplicating message events at the EventHandler level
        /// <see href="https://learn.microsoft.com/en-us/dotnet/architecture/microservices/multi-container-microservice-net-applications/subscribe-events#deduplicating-message-events-at-the-eventhandler-level"/>
        /// </summary>
        public IdentifiedCommand(TRequest command, Guid id)
        {
            Command = command;
            Id = id;
        }
    }

    public class IdentifiedCommand<TRequest, TResponse> : IRequest<TResponse>, IIdentifiedRequest<TRequest>
        where TRequest : IRequest<TResponse>
    {
        public TRequest Command { get; }
        public Guid Id { get; }

        /// <summary>
        /// Deduplicating message events at the EventHandler level
        /// <see href="https://learn.microsoft.com/en-us/dotnet/architecture/microservices/multi-container-microservice-net-applications/subscribe-events#deduplicating-message-events-at-the-eventhandler-level"/>
        /// </summary>
        public IdentifiedCommand(TRequest command, Guid id)
        {
            Command = command;
            Id = id;
        }
    }

}
