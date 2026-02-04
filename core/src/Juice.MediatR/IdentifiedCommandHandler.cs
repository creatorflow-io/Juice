using System.Diagnostics;
using Juice.MediatR.Extensions;
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

        /// <summary>
        /// Extracts debug information from the command to be used in logging
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        protected virtual (string? IdProperty, string? CommandId) ExtractDebugInfo(T command)
            => (default, default);

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

        protected async Task DispatchAsync(TRequest command, CancellationToken cancellationToken)
        {
            var (idProperty, commandId) = ExtractDebugInfo(command);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "----- Sending command: {CommandName} - {IdProperty}: {CommandId} ({@Command})",
                    command.GetGenericTypeName(),
                    idProperty ?? "ExtractDebugInfo not implemented",
                    commandId ?? "ExtractDebugInfo not implemented",
                    command);
            }

            // Send the embeded business command to mediator so it runs its related CommandHandler 
            await _mediator.Send(command, cancellationToken);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "----- Command sent: {CommandName} - {IdProperty}: {CommandId} ({@Command})",
                    command.GetGenericTypeName(),
                    idProperty ?? "ExtractDebugInfo not implemented",
                    commandId ?? "ExtractDebugInfo not implemented",
                    command);
            }
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
                return;
            }
            try
            {
                await DispatchAsync(message.Command, cancellationToken);
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

        protected async Task<TResponse> DispatchAsync(TRequest command, CancellationToken cancellationToken)
        {
            var (idProperty, commandId) = ExtractDebugInfo(command);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "----- Sending command: {CommandName} - {IdProperty}: {CommandId} ({@Command})",
                    command.GetGenericTypeName(),
                    idProperty ?? "ExtractDebugInfo not implemented",
                    commandId ?? "ExtractDebugInfo not implemented",
                    command);
            }

            // Send the embeded business command to mediator so it runs its related CommandHandler 
            var result = await _mediator.Send(command, cancellationToken);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "----- Command result: {@Result} - {CommandName} - {IdProperty}: {CommandId} ({@Command})",
                    result,
                    command.GetGenericTypeName(),
                    idProperty ?? "ExtractDebugInfo not implemented",
                    commandId ?? "ExtractDebugInfo not implemented",
                    command);
            }
            return result;
        }


        /// <summary>
        /// Creates the result value to return if a previous request was found but no cached result was stored.
        /// </summary>
        /// <returns></returns>
        protected virtual ValueTask<TResponse> CreateResultForDuplicatedRequestAsync(TRequest command)
        {
            return ValueTask.FromResult<TResponse>(default!);
        }


        protected virtual async Task<TResponse> HandleDuplicatedRequestAsync(TRequest command, Guid requestId)
        {
            var result = await _requestManager.GetCachedResultAsync<TRequest, TResponse>(requestId);
            if (result is not null)
            {
                return result;
            }
            // It means that no cached result was stored, so we create a default one
            return await CreateResultForDuplicatedRequestAsync(command);
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
            var stopwatch = Stopwatch.StartNew();
            var commandName = message.Command.GetGenericTypeName();
            MediatorMetrics.IncrementIdentifiedCommandReceived(commandName);
            var created = await _requestManager.TryCreateRequestForCommandAsync<TRequest>(message.Id);
            if (!created)
            {
                MediatorMetrics.IncrementIdentifiedCommandDuplicated(commandName);
                return await HandleDuplicatedRequestAsync(message.Command, message.Id);
            }
            try
            {
                var result = await DispatchAsync(message.Command, cancellationToken);
                await _requestManager.TryCompleteRequestAsync<TRequest>(message.Id, true, result);
                MediatorMetrics.IncrementIdentifiedCommandProcessed(commandName);
                return result;
            }
            catch (Exception)
            {
                await _requestManager.TryCompleteRequestAsync<TRequest>(message.Id, false);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                MediatorMetrics.RecordIdentifiedCommandLatency(commandName, stopwatch.Elapsed);
            }
        }
    }
}
