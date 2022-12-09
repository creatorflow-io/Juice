using Juice.Extensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Juice.MediatR
{
    /// <summary>
    /// Provides a base implementation for handling duplicate request and ensuring idempotent updates, in the cases where
    /// a requestid sent by client is used to detect duplicate requests.
    /// </summary>
    /// <typeparam name="T">Type of the command handler that performs the operation if request is not duplicated</typeparam>
    /// <typeparam name="R">Return value of the inner command handler</typeparam>
    public abstract class IdentifiedCommandHandler<T, R> : IRequestHandler<IdentifiedCommand<T, R>, R>
        where T : IRequest<R>
    {
        private readonly IMediator _mediator;
        private readonly IRequestManager _requestManager;
        private readonly ILogger _logger;

        public IdentifiedCommandHandler(
            IMediator mediator,
            IRequestManager requestManager,
            ILogger logger)
        {
            _mediator = mediator;
            _requestManager = requestManager;
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
        }


        /// <summary>
        /// Creates the result value to return if a previous request was found
        /// </summary>
        /// <returns></returns>
        protected virtual R CreateResultForDuplicateRequest()
        {
            return default;
        }

        /// <summary>
        /// This method handles the command. It just ensures that no other request exists with the same ID, and if this is the case
        /// just enqueues the original inner command.
        /// </summary>
        /// <param name="message">IdentifiedCommand which contains both original command & request ID</param>
        /// <returns>Return value of inner command or default value if request same ID was found</returns>
        public async Task<R> Handle(IdentifiedCommand<T, R> message, CancellationToken cancellationToken)
        {

            var created = await _requestManager.TryCreateRequestForCommandAsync<T, R>(message.Id);
            if (!created)
            {
                return CreateResultForDuplicateRequest();
            }
            try
            {
                var command = message.Command;
                var commandName = command.GetGenericTypeName();

                var (idProperty, commandId) = ExtractInfo(command);

                _logger.LogInformation(
                    "----- Sending command: {CommandName} - {IdProperty}: {CommandId} ({@Command})",
                    commandName,
                    idProperty,
                    commandId,
                    command);

                // Send the embeded business command to mediator so it runs its related CommandHandler 
                var result = await _mediator.Send(command, cancellationToken);

                _logger.LogInformation(
                    "----- Command result: {@Result} - {CommandName} - {IdProperty}: {CommandId} ({@Command})",
                    result,
                    commandName,
                    idProperty,
                    commandId,
                    command);
                await _requestManager.TryCompleteRequestAsync(message.Id, true);
                return result;
            }
            catch
            {
                await _requestManager.TryCompleteRequestAsync(message.Id, true);
                return default(R);
            }
        }

        protected abstract (string IdProperty, string CommandId) ExtractInfo(T command);
    }
}
