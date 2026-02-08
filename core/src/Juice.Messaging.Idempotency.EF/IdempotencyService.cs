using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Juice.Messaging.Idempotency.EF
{
    internal class IdempotencyService: IIdempotencyService
    {
        private IdempotencyContext _context;
        private readonly ILogger _logger;
        private readonly IMessageSerializer _serializer;
        public IdempotencyService(IdempotencyContext context, ILogger<IdempotencyService> logger, IMessageSerializer serializer)
        {
            _context = context;
            _logger = logger;
            _serializer = serializer;
        }

        public async ValueTask TryCompleteRequestAsync(string scope, string key, bool success, object? result, CancellationToken cancellationToken)
        {
            try
            {
                var res = _serializer.Serialize(result);
                await
                    _context.IdempotencyRecords.Where(r => r.Scope == scope && r.Key == key)
                    .ExecuteUpdateAsync(r =>
                        r.SetProperty(r => r.State,
                        success ? RequestState.Processed : RequestState.Failed)
                        .SetProperty(r => r.CompletedAt, DateTimeOffset.Now)
                        .SetProperty(r => r.Result, res)
                        , cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing request {RequestId} for scope {CommandName}", key, scope);
            }
        }

        public async ValueTask<IOperationResult> TryCreateRequestAsync(string scope, string key, CancellationToken cancellationToken)
        {
            var record = await _context.IdempotencyRecords.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Scope == scope && r.Key == key, cancellationToken);

            if (record == null)
            {
                try
                {
                    _context.IdempotencyRecords.Add(new IdempotencyRecord(scope, key));
                    await _context.SaveChangesAsync(cancellationToken);
                    return OperationResult.Success;
                }
                catch (DbUpdateException)
                {
                    return await TryRetryAsync(scope, key, cancellationToken);
                }
            }
            if(record.State == RequestState.Failed)
            {
                return await TryRetryAsync(scope, key, cancellationToken);
            }
            return OperationResult.Failed($"The record with scope '{scope}' and key '{key}' already exists.");
        }

        public async ValueTask<IOperationResult<TResponse>> TryCreateRequestAsync<TResponse>(string scope, string key, CancellationToken cancellationToken)
        {
            var record = await _context.IdempotencyRecords.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Scope == scope && r.Key == key, cancellationToken);

            if (record == null)
            {
                try
                {
                    _context.IdempotencyRecords.Add(new IdempotencyRecord(scope, key));
                    await _context.SaveChangesAsync(cancellationToken);
                    return OperationResult.Result<TResponse>(default);
                }
                catch (DbUpdateException)
                {
                    return await TryCreateRequestAsync<TResponse>(scope, key, cancellationToken);
                }
            }
            if (record.State == RequestState.Failed)
            {
                return (await TryRetryAsync(scope, key, cancellationToken)).Of<TResponse>();
            }
            if (record.State == RequestState.Processed && record.Result != null)
            {
                try
                {
                    var result = _serializer.Deserialize<TResponse>(record.Result, default);
                    return OperationResult.Failed($"The record with scope '{scope}' and key '{key}' already exists.", result);
                }catch(Exception ex)
                {
                    return OperationResult.Failed<TResponse>(ex);
                }
            }
            return OperationResult.Failed<TResponse>($"The record with scope '{scope}' and key '{key}' already exists.");
        }

        private async Task<IOperationResult> TryRetryAsync(string scope, string key, CancellationToken cancellationToken)
        {
            var updated = await _context.IdempotencyRecords
                                .Where(r => r.Scope == scope
                                         && r.Key == key
                                         && r.State == RequestState.Failed)
                                .ExecuteUpdateAsync(s => s
                                    .SetProperty(r => r.State, RequestState.New), cancellationToken);

            return updated == 1 ? OperationResult.Success
                : OperationResult.Failed($"The record with scope '{scope}' and key '{key}' already exists.");
        }

    }

}
