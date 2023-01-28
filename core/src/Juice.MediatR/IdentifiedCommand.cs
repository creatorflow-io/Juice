using MediatR;

namespace Juice.MediatR
{

    public class IdentifiedCommand<T> : IRequest<IOperationResult?>
        where T : IRequest<IOperationResult>
    {
        public T Command { get; }
        public Guid Id { get; }
        /// <summary>
        /// Deduplicating message events at the EventHandler level
        /// <see href="https://learn.microsoft.com/en-us/dotnet/architecture/microservices/multi-container-microservice-net-applications/subscribe-events#deduplicating-message-events-at-the-eventhandler-level"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public IdentifiedCommand(T command, Guid id)
        {
            Command = command;
            Id = id;
        }
    }

    public class IdentifiedCommand<T, R> : IRequest<IOperationResult<R?>>
        where T : IRequest<R>
    {
        public T Command { get; }
        public Guid Id { get; }
        /// <summary>
        /// NOTE: If <see cref="R"/> is <see cref="IOperationResult"/> use <see cref="IdentifiedCommand{T}"/> instead.<para></para>
        /// Deduplicating message events at the EventHandler level
        /// <see href="https://learn.microsoft.com/en-us/dotnet/architecture/microservices/multi-container-microservice-net-applications/subscribe-events#deduplicating-message-events-at-the-eventhandler-level"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public IdentifiedCommand(T command, Guid id)
        {
            Command = command;
            if (typeof(R).IsAssignableTo(typeof(IOperationResult)))
            {
                throw new InvalidDataException("Use IdentifiedCommand<T> instead.");
            }
            Id = id;
        }
    }

    public class IdentifiedCommand<T, R, D> : IRequest<R>
        where T : IRequest<R>
        where R : IOperationResult<D>
    {
        public T Command { get; }
        public Guid Id { get; }

        /// <summary>
        /// Deduplicating message events at the EventHandler level
        /// <see href="https://learn.microsoft.com/en-us/dotnet/architecture/microservices/multi-container-microservice-net-applications/subscribe-events#deduplicating-message-events-at-the-eventhandler-level"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="R"></typeparam>
        public IdentifiedCommand(T command, Guid id)
        {
            Command = command;
            Id = id;
        }
    }
}
