using Juice.Extensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Juice.MediatR
{
    public abstract class IdentifiedCommandHandlerBase<T, R>
        where T : IRequest<R>
    {
        protected readonly IMediator _mediator;
        protected readonly IRequestManager _requestManager;
        protected readonly ILogger _logger;
        public IdentifiedCommandHandlerBase(
            IMediator mediator,
            IRequestManager requestManager,
            ILogger logger)
        {
            _mediator = mediator;
            _requestManager = requestManager;
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
        }

        protected async Task<R> DispatchAsync(T command, Guid messageId, CancellationToken cancellationToken)
        {

            var commandName = command.GetGenericTypeName();

            var (idProperty, commandId) = ExtractInfo(command);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "----- Sending command: {CommandName} - {IdProperty}: {CommandId} ({@Command})",
                    commandName,
                    idProperty,
                    commandId,
                    command);
            }

            // Send the embeded business command to mediator so it runs its related CommandHandler 
            var result = await _mediator.Send(command, cancellationToken);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "----- Command result: {@Result} - {CommandName} - {IdProperty}: {CommandId} ({@Command})",
                    result,
                    commandName,
                    idProperty,
                    commandId,
                    command);
            }
            await _requestManager.TryCompleteRequestAsync(messageId, true);
            return result;
        }

        protected abstract (string IdProperty, string CommandId) ExtractInfo(T command);

    }
    /// <summary>
    /// Provides a base implementation for handling duplicate request and ensuring idempotent updates, in the cases where
    /// a requestid sent by client is used to detect duplicate requests.
    /// </summary>
    /// <typeparam name="T">Type of the command handler that performs the operation if request is not duplicated</typeparam>
    /// <typeparam name="R">Return value of the inner command handler</typeparam>
    public abstract class IdentifiedCommandHandler<T>
        : IdentifiedCommandHandlerBase<T, IOperationResult?>
        , IRequestHandler<IdentifiedCommand<T>, IOperationResult?>
        where T : IRequest<IOperationResult>
    {

        public IdentifiedCommandHandler(
            IMediator mediator,
            IRequestManager requestManager,
            ILogger logger) : base(mediator, requestManager, logger)
        {

        }

        /// <summary>
        /// Creates the result value to return if a previous request was found
        /// </summary>
        /// <returns></returns>
        protected abstract Task<IOperationResult?> CreateResultForDuplicateRequestAsync(IdentifiedCommand<T> message);

        /// <summary>
        /// This method handles the command. It just ensures that no other request exists with the same ID, and if this is the case
        /// just enqueues the original inner command.
        /// </summary>
        /// <param name="message">IdentifiedCommand which contains both original command & request ID</param>
        /// <returns>Return value of inner command or default value if request same ID was found</returns>
        public async Task<IOperationResult> Handle(IdentifiedCommand<T> message, CancellationToken cancellationToken)
        {

            var created = await _requestManager.TryCreateRequestForCommandAsync<T, IOperationResult>(message.Id);
            if (!created)
            {
                _logger.LogInformation("Return result for duplicated request.");
                return await CreateResultForDuplicateRequestAsync(message);
            }
            try
            {
                return await DispatchAsync(message.Command, message.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                await _requestManager.TryCompleteRequestAsync(message.Id, false);
                return OperationResult.Failed(ex);
            }
        }

    }

    /// <summary>
    /// NOTE: If R is <see cref="IOperationResult"/> use <see cref="IdentifiedCommand{T}"/> instead.<para></para>
    /// Provides a base implementation for handling duplicate request and ensuring idempotent updates, in the cases where
    /// a requestid sent by client is used to detect duplicate requests.
    /// </summary>
    /// <typeparam name="T">Type of the command handler that performs the operation if request is not duplicated</typeparam>
    /// <typeparam name="R">Return value of the inner command handler.</typeparam>
    public abstract class IdentifiedCommandHandler<T, R> :
        IdentifiedCommandHandlerBase<T, R>,
        IRequestHandler<IdentifiedCommand<T, R>, IOperationResult<R?>>
        where T : IRequest<R>
        // where R is not IOperationResult
    {

        public IdentifiedCommandHandler(
            IMediator mediator,
            IRequestManager requestManager,
            ILogger logger) : base(mediator, requestManager, logger)
        {
        }


        /// <summary>
        /// Creates the result value to return if a previous request was found
        /// </summary>
        /// <returns></returns>
        protected abstract Task<R?> CreateResultForDuplicateRequestAsync(IdentifiedCommand<T, R> message);

        /// <summary>
        /// This method handles the command. It just ensures that no other request exists with the same ID, and if this is the case
        /// just enqueues the original inner command.
        /// </summary>
        /// <param name="message">IdentifiedCommand which contains both original command & request ID</param>
        /// <returns>Return value of inner command or default value if request same ID was found</returns>
        public async Task<IOperationResult<R?>> Handle(IdentifiedCommand<T, R> message, CancellationToken cancellationToken)
        {

            var created = await _requestManager.TryCreateRequestForCommandAsync<T, R>(message.Id);
            if (!created)
            {
                var data = await CreateResultForDuplicateRequestAsync(message);
                return OperationResult.Result(data);
            }
            try
            {
                var result = await DispatchAsync(message.Command, message.Id, cancellationToken);
                return OperationResult.Result(result);
            }
            catch (Exception ex)
            {
                await _requestManager.TryCompleteRequestAsync(message.Id, false);
                return OperationResult.Failed<R>(ex);
            }
        }

    }

    /// <summary>
    /// Provides a base implementation for handling duplicate request and ensuring idempotent updates, in the cases where
    /// a requestid sent by client is used to detect duplicate requests.
    /// </summary>
    /// <typeparam name="T">Type of the command handler that performs the operation if request is not duplicated</typeparam>
    /// <typeparam name="R">Return value of the inner command handler</typeparam>
    public abstract class IdentifiedCommandHandler<T, R, D>
        : IdentifiedCommandHandlerBase<T, R>,
        IRequestHandler<IdentifiedCommand<T, R, D>, R>
        where T : IRequest<R>
        where R : IOperationResult<D>
    {

        public IdentifiedCommandHandler(
            IMediator mediator,
            IRequestManager requestManager,
            ILogger logger) : base(mediator, requestManager, logger)
        {

        }


        /// <summary>
        /// Creates the result value to return if a previous request was found
        /// </summary>
        /// <returns></returns>
        protected abstract Task<D> CreateResultForDuplicateRequestAsync(IdentifiedCommand<T, R, D> message);

        /// <summary>
        /// This method handles the command. It just ensures that no other request exists with the same ID, and if this is the case
        /// just enqueues the original inner command.
        /// </summary>
        /// <param name="message">IdentifiedCommand which contains both original command & request ID</param>
        /// <returns>Return value of inner command or default value if request same ID was found</returns>
        public async Task<R> Handle(IdentifiedCommand<T, R, D> message, CancellationToken cancellationToken)
        {

            var created = await _requestManager.TryCreateRequestForCommandAsync<T, R>(message.Id);
            if (!created)
            {
                var data = await CreateResultForDuplicateRequestAsync(message);
                return (R)Convert.ChangeType(OperationResult.Result(data), typeof(R));
            }
            try
            {
                return await DispatchAsync(message.Command, message.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                await _requestManager.TryCompleteRequestAsync(message.Id, false);
                return (R)Convert.ChangeType(OperationResult.Failed<R>(ex), typeof(R));
            }
        }

    }
}
