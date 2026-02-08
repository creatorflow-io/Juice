using System.Diagnostics;
using Juice.Extensions;
using Juice.Messaging.Idempotency;
using Microsoft.Extensions.Logging;

namespace Juice.MediatR.Behaviors
{
    public class IdempotencyRequestBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>, IIdempotentRequest
    {
        public int Order => int.MinValue; // run very early
        private readonly ILogger _logger;
        private readonly IIdempotencyService _idempotencyService;
        public IdempotencyRequestBehavior(
            IIdempotencyService inboxService,
            ILogger<IdempotencyRequestBehavior<TRequest, TResponse>> logger)
        {
            _logger = logger;
            _idempotencyService = inboxService;
        }
        public async ValueTask<TResponse> Handle(TRequest request, RequestHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var commandName = request.GetGenericTypeName();
            MediatorMetrics.IncrementIdentifiedCommandReceived(commandName);
            var create = await _idempotencyService.TryCreateRequestAsync<TResponse>(typeof(TRequest).Name, request.IdempotencyKey, cancellationToken);
            if (!create.Succeeded)
            {
                MediatorMetrics.IncrementIdentifiedCommandDuplicated(commandName);
                return create.Data ?? default!;
            }
            try
            {
                TResponse result = await next.Invoke(request, cancellationToken);
                await _idempotencyService.TryCompleteRequestAsync(typeof(TRequest).Name, request.IdempotencyKey, true, result, cancellationToken);
                MediatorMetrics.IncrementIdentifiedCommandProcessed(commandName);
                return result;
            }
            catch (Exception)
            {
                await _idempotencyService.TryCompleteRequestAsync(typeof(TRequest).Name, request.IdempotencyKey, false, cancellationToken: cancellationToken);
                MediatorMetrics.IncrementIdentifiedCommandFailed(commandName);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                MediatorMetrics.RecordIdentifiedCommandLatency(commandName, stopwatch.Elapsed);
            }
        }

    }

    public class IdempotencyRequestBehavior<TRequest> : IPipelineBehavior<TRequest>
        where TRequest : IRequest, IIdempotentRequest
    {
        public int Order => int.MinValue; // run very early
        private readonly ILogger _logger;
        private readonly IIdempotencyService _idempotencyService;
        public IdempotencyRequestBehavior(
            IIdempotencyService inboxService,
            ILogger<IdempotencyRequestBehavior<TRequest>> logger)
        {
            _logger = logger;
            _idempotencyService = inboxService;
        }
        public async ValueTask Handle(TRequest request, RequestHandlerDelegate<TRequest> next, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var commandName = request.GetGenericTypeName();
            MediatorMetrics.IncrementIdentifiedCommandReceived(commandName);
            var created = await _idempotencyService.TryCreateRequestAsync(typeof(TRequest).Name, request.IdempotencyKey);
            if (!created.Succeeded)
            {
                MediatorMetrics.IncrementIdentifiedCommandDuplicated(commandName);
                _logger.LogWarning("Duplicate request detected for {CommandName} with Id {RequestId}. Ignoring execution.", commandName, request.MessageId);
                return;
            }
            try
            {
                await next.Invoke(request, cancellationToken);
                await _idempotencyService.TryCompleteRequestAsync(typeof(TRequest).Name, request.IdempotencyKey, true, cancellationToken: cancellationToken);
                MediatorMetrics.IncrementIdentifiedCommandProcessed(commandName);
                return;
            }
            catch (Exception)
            {
                await _idempotencyService.TryCompleteRequestAsync(typeof(TRequest).Name, request.IdempotencyKey, false, cancellationToken: cancellationToken);
                MediatorMetrics.IncrementIdentifiedCommandFailed(commandName);
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
