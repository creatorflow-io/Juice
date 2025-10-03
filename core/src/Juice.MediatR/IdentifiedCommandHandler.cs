using Juice.MediatR.Extensions;
using Juice.MediatR.Internal;
using Microsoft.Extensions.Logging;

namespace Juice.MediatR
{
    public abstract class IdentifiedCommandHandlerBase<T, R>
        where T : IBaseRequest
    {
        protected readonly IMediator _mediator;
        protected readonly IRequestManagerBase _requestManager;
        protected readonly ILogger _logger;
        public IdentifiedCommandHandlerBase(
            IMediator mediator,
            IRequestManagerBase requestManager,
            ILogger logger)
        {
            _mediator = mediator;
            _requestManager = requestManager;
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
        }

        protected async Task<R> DispatchAsync(T command, Guid messageId, CancellationToken cancellationToken)
        {

            var commandName = command.GetGenericTypeName();

            var (idProperty, commandId) = ExtractDebugInfo(command);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "----- Sending command: {CommandName} - {IdProperty}: {CommandId} ({@Command})",
                    commandName,
                    idProperty ?? "ExtractDebugInfo not implemented",
                    commandId ?? "ExtractDebugInfo not implemented",
                    command);
            }

            // Send the embeded business command to mediator so it runs its related CommandHandler 
            var result =
                command is IRequest<R> requestWithResult ? await _mediator.Send(requestWithResult, cancellationToken)
                : command is IRequest request ? await SendWrapperAsync(request, cancellationToken)
                : throw new InvalidOperationException($"Command {commandName} does not implement IRequest<R>, IRequest<IOperationResult<R>> or IRequest");
            ;
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "----- Command result: {@Result} - {CommandName} - {IdProperty}: {CommandId} ({@Command})",
                    result,
                    commandName,
                    idProperty ?? "ExtractDebugInfo not implemented",
                    commandId ?? "ExtractDebugInfo not implemented",
                    command);
            }
            return result;
        }

        private async ValueTask<R> SendWrapperAsync(IRequest command, CancellationToken cancellationToken)
        {
            if (!typeof(R).IsAssignableTo(typeof(IOperationResult)))
            {
                throw new InvalidOperationException($"Command {command.GetGenericTypeName()} does not implement IRequest<R> or IRequest<IOperationResult<R>>. Unable to convert void result to {typeof(R).Name}");
            }
            try
            {
                await _mediator.Send(command, cancellationToken);
                return (R)Convert.ChangeType(new OperationResult { Succeeded = true }, typeof(R));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending command: {CommandName}", command.GetGenericTypeName());
                return (R)Convert.ChangeType(new OperationResult { Message = ex.Message }, typeof(R));
            }
        }

        /// <summary>
        /// Extracts debug information from the command to be used in logging
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        protected virtual (string? IdProperty, string? CommandId) ExtractDebugInfo(T command)
            => (default, default);


        /// <summary>
        /// Creates the result value to return if a previous request was found
        /// </summary>
        /// <returns></returns>
        protected abstract ValueTask<R> CreateResultForDuplicatedRequestAsync(T command);

    }

    /// <summary>
    /// <para>Handle <see cref="IdentifiedCommand{TRequest}"/></para>
    /// Provides a base implementation for handling duplicate request and ensuring idempotent updates, in the cases where
    /// a requestid sent by client is used to detect duplicate requests.
    /// </summary>
    /// <typeparam name="TRequest">Type of the command handler that performs the operation if request is not duplicated</typeparam>
    public abstract class IdentifiedCommandHandler<TRequest>
        : IdentifiedCommandHandlerBase<TRequest, IOperationResult>,
        IRequestHandler<IdentifiedCommand<TRequest>>
        where TRequest : IRequest
    {

        public IdentifiedCommandHandler(
            IMediator mediator,
            IRequestManagerBase requestManager,
            ILogger logger) : base(mediator, requestManager, logger)
        {

        }


        /// <summary>
        /// This method handles the command. It just ensures that no other request exists with the same ID, and if this is the case
        /// just enqueues the original inner command.
        /// </summary>
        /// <param name="message">IdentifiedCommand which contains both original command and request ID</param>
        /// <param name="cancellationToken"></param>
        /// <returns>Return value of inner command or default value if request same ID was found</returns>
        public async ValueTask Handle(IdentifiedCommand<TRequest> message, CancellationToken cancellationToken)
        {

            var created = await _requestManager.TryCreateRequestForCommandAsync<TRequest>(message.Id);
            if (!created)
            {
                var result = await CreateResultForDuplicatedRequestAsync(message.Command);
                if (!result.Succeeded)
                {
                    throw new Exception($"Request for {message.Command.GetGenericTypeName()} failed. {result.Message}");
                }
                return;
            }
            try
            {
                await DispatchAsync(message.Command, message.Id, cancellationToken);
                await _requestManager.TryCompleteRequestAsync<TRequest>(message.Id, true);
            }
            catch (Exception)
            {
                await _requestManager.TryCompleteRequestAsync<TRequest>(message.Id, false);
                throw;
            }
        }

    }

    /// <summary>
    /// <para>Handle <see cref="IdentifiedCommand{TRequest,TResponse}"/></para>
    /// Provides a base implementation for handling duplicate request and ensuring idempotent updates, in the cases where
    /// a requestid sent by client is used to detect duplicate requests.
    /// </summary>
    public abstract class IdentifiedCommandHandler<TRequest, TResponse>
        : IdentifiedCommandHandlerBase<TRequest, TResponse>,
        IRequestHandler<IdentifiedCommand<TRequest, TResponse>, TResponse>
        where TRequest : IRequest<TResponse>
    {

        public IdentifiedCommandHandler(
            IMediator mediator,
            IRequestManagerBase requestManager,
            ILogger logger) : base(mediator, requestManager, logger)
        {

        }

        /// <summary>
        /// This method handles the command. It just ensures that no other request exists with the same ID, and if this is the case
        /// just enqueues the original inner command.
        /// </summary>
        /// <param name="message">IdentifiedCommand which contains both original command and request ID</param>
        /// <param name="cancellationToken"></param>
        /// <returns>Return value of inner command or default value if request same ID was found</returns>
        public async ValueTask<TResponse> Handle(IdentifiedCommand<TRequest, TResponse> message, CancellationToken cancellationToken)
        {

            var created = await _requestManager.TryCreateRequestForCommandAsync<TRequest>(message.Id);
            if (!created)
            {
                return await CreateResultForDuplicatedRequestAsync(message.Command);
            }
            try
            {
                var result = await DispatchAsync(message.Command, message.Id, cancellationToken);
                await _requestManager.TryCompleteRequestAsync<TRequest>(message.Id, true);
                return result;
            }
            catch (Exception)
            {
                await _requestManager.TryCompleteRequestAsync<TRequest>(message.Id, false);
                throw;
            }
        }

    }
}
