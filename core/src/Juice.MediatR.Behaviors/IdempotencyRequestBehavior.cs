using System.Diagnostics;
using Juice.Extensions;
using Juice.Messaging;
using Juice.Messaging.Idempotency;
using Microsoft.Extensions.Logging;

namespace Juice.MediatR.Behaviors
{
    /// <summary>
    /// Shared idempotency flow for both the void and value-returning behaviors. Begins the request through
    /// <see cref="IIdempotencyService.TryBeginRequestAsync"/> so records participate in the store's
    /// retention/recovery lifecycle (expiry, in-flight lock), and maps the outcome:
    /// <list type="bullet">
    /// <item><see cref="IdempotencyOutcome.Created"/> — the caller executes the handler.</item>
    /// <item><see cref="IdempotencyOutcome.Completed"/> — replay the stored result; do not re-execute.</item>
    /// <item><see cref="IdempotencyOutcome.InProgress"/> — throw <see cref="DuplicateRequestInProgressException"/>.</item>
    /// <item><see cref="IdempotencyOutcome.Conflict"/> — throw <see cref="IdempotencyKeyConflictException"/>.</item>
    /// </list>
    /// </summary>
    internal static class IdempotencyBehaviorCore
    {
        /// <summary>
        /// Begins (or resumes) the request. Increments the duplicated counter and throws for
        /// InProgress/Conflict; returns the raw outcome so the caller can replay a Completed result or
        /// proceed on Created.
        /// </summary>
        public static async ValueTask<IdempotencyResult> BeginAsync(
            IIdempotencyService service,
            string commandName,
            string scope,
            string key,
            string? requestHash,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var begin = await service.TryBeginRequestAsync(scope, key, requestHash, cancellationToken);
            switch (begin.Outcome)
            {
                case IdempotencyOutcome.InProgress:
                    MediatorMetrics.IncrementIdentifiedCommandDuplicated(commandName);
                    logger.LogWarning(
                        "Duplicate request in progress for {CommandName} (scope {Scope}, key {Key}). Signalling retry.",
                        commandName, scope, key);
                    throw new DuplicateRequestInProgressException(scope, key);

                case IdempotencyOutcome.Conflict:
                    MediatorMetrics.IncrementIdentifiedCommandDuplicated(commandName);
                    logger.LogWarning(
                        "Idempotency key conflict for {CommandName} (scope {Scope}, key {Key}). Rejecting.",
                        commandName, scope, key);
                    throw new IdempotencyKeyConflictException(scope, key);

                case IdempotencyOutcome.Completed:
                    MediatorMetrics.IncrementIdentifiedCommandDuplicated(commandName);
                    break;
            }

            return begin;
        }
    }

    public class IdempotencyRequestBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>, IIdempotentRequest
    {
        public int Order => int.MinValue; // run very early
        private readonly ILogger _logger;
        private readonly IIdempotencyService _idempotencyService;
        private readonly IMessageSerializer _serializer;
        private readonly IIdempotencyScopeProvider _scopeProvider;
        public IdempotencyRequestBehavior(
            IIdempotencyService inboxService,
            IMessageSerializer serializer,
            IIdempotencyScopeProvider scopeProvider,
            ILogger<IdempotencyRequestBehavior<TRequest, TResponse>> logger)
        {
            _logger = logger;
            _idempotencyService = inboxService;
            _serializer = serializer;
            _scopeProvider = scopeProvider;
        }
        public async ValueTask<TResponse> Handle(TRequest request, RequestHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var commandName = request.GetGenericTypeName();
            var scope = _scopeProvider.Resolve(typeof(TRequest).Name);           // tenant-partitioned (P2)
            var requestHash = IdempotencyFingerprint.Compute(request, _serializer); // payload conflict (P3)
            MediatorMetrics.IncrementIdentifiedCommandReceived(commandName);
            try
            {
                var begin = await IdempotencyBehaviorCore.BeginAsync(
                    _idempotencyService, commandName, scope, request.IdempotencyKey, requestHash, _logger, cancellationToken);

                if (begin.Outcome == IdempotencyOutcome.Completed)
                {
                    // Replay the originally stored result without re-executing.
                    return string.IsNullOrEmpty(begin.StoredResult)
                        ? default!
                        : _serializer.Deserialize<TResponse>(begin.StoredResult, default) ?? default!;
                }

                // Created: execute, then capture the result for future replays.
                TResponse result = await next.Invoke(request, cancellationToken);
                await _idempotencyService.TryCompleteRequestAsync(scope, request.IdempotencyKey, true, result, cancellationToken);
                MediatorMetrics.IncrementIdentifiedCommandProcessed(commandName);
                return result;
            }
            catch (DuplicateRequestInProgressException)
            {
                throw; // in-flight duplicate: no record of ours to reset, just signal retry.
            }
            catch (IdempotencyKeyConflictException)
            {
                throw; // payload conflict: not applied, nothing to reset.
            }
            catch (Exception)
            {
                await _idempotencyService.TryCompleteRequestAsync(scope, request.IdempotencyKey, false, cancellationToken: cancellationToken);
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
        private readonly IMessageSerializer _serializer;
        private readonly IIdempotencyScopeProvider _scopeProvider;
        public IdempotencyRequestBehavior(
            IIdempotencyService inboxService,
            IMessageSerializer serializer,
            IIdempotencyScopeProvider scopeProvider,
            ILogger<IdempotencyRequestBehavior<TRequest>> logger)
        {
            _logger = logger;
            _idempotencyService = inboxService;
            _serializer = serializer;
            _scopeProvider = scopeProvider;
        }
        public async ValueTask Handle(TRequest request, RequestHandlerDelegate<TRequest> next, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var commandName = request.GetGenericTypeName();
            var scope = _scopeProvider.Resolve(typeof(TRequest).Name);           // tenant-partitioned (P2)
            var requestHash = IdempotencyFingerprint.Compute(request, _serializer); // payload conflict (P3)
            MediatorMetrics.IncrementIdentifiedCommandReceived(commandName);
            try
            {
                var begin = await IdempotencyBehaviorCore.BeginAsync(
                    _idempotencyService, commandName, scope, request.IdempotencyKey, requestHash, _logger, cancellationToken);

                if (begin.Outcome == IdempotencyOutcome.Completed)
                {
                    // Already applied; skip execution (void request carries no result to replay).
                    return;
                }

                // Created: execute, then mark completed.
                await next.Invoke(request, cancellationToken);
                await _idempotencyService.TryCompleteRequestAsync(scope, request.IdempotencyKey, true, cancellationToken: cancellationToken);
                MediatorMetrics.IncrementIdentifiedCommandProcessed(commandName);
                return;
            }
            catch (DuplicateRequestInProgressException)
            {
                throw; // in-flight duplicate: no record of ours to reset, just signal retry.
            }
            catch (IdempotencyKeyConflictException)
            {
                throw; // payload conflict: not applied, nothing to reset.
            }
            catch (Exception)
            {
                await _idempotencyService.TryCompleteRequestAsync(scope, request.IdempotencyKey, false, cancellationToken: cancellationToken);
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
