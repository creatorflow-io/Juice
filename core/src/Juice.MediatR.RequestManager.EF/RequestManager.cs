using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Juice.MediatR.RequestManager.EF
{
    internal class RequestManagerBase : IRequestManagerBase
    {
        private ClientRequestContextBase _context;
        private readonly ILogger _logger;
        private readonly IResponseSerializer _serializer;
        public RequestManagerBase(ClientRequestContextBase context, ILogger logger, IResponseSerializer serializer)
        {
            _context = context;
            _logger = logger;
            _serializer = serializer;
        }

        public async ValueTask TryCompleteRequestAsync<T>(Guid id, bool success, object? result)
            where T : IBaseRequest
        {
            try
            {
                var res = _serializer.SerializeResponse(result);
                await _context.ClientRequests.Where(r => r.Id == id && r.Name == typeof(T).Name)
                    .ExecuteUpdateAsync(r =>
                        r.SetProperty(r => r.State,
                        success ? RequestState.Processed : RequestState.ProcessedFailed)
                        .SetProperty(r => r.CompletedTime, DateTimeOffset.Now)
                        .SetProperty(r => r.Result, res)
                        );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing request {RequestId} for command {CommandName}", id, typeof(T).Name);
            }
        }

        public async ValueTask<bool> TryCreateRequestForCommandAsync<T>(Guid id)
            where T : IBaseRequest
        {
            try
            {
                _context.ClientRequests.Add(new ClientRequest(id, typeof(T).Name));
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException)
            {
                return await TryRetryAsync(id, typeof(T).Name);
            }
        }

        private async Task<bool> TryRetryAsync(Guid id, string commandName)
        {
            var updated = await _context.ClientRequests
                                .Where(r => r.Id == id
                                         && r.Name == commandName
                                         && r.State == RequestState.ProcessedFailed)
                                .ExecuteUpdateAsync(s => s
                                    .SetProperty(r => r.State, RequestState.New)
                                    .SetProperty(r => r.Time, DateTimeOffset.UtcNow));

            return updated == 1;
        }

        public async ValueTask<TR?> GetCachedResultAsync<T, TR>(Guid id)
            where T : IBaseRequest
        {
            var res = await _context.ClientRequests
                        .Where(r => r.Id == id && r.State == RequestState.Processed)
                        .Select(r => r.Result)
                        .FirstOrDefaultAsync();
            return _serializer.DeserializeResponse<TR>(res);
        }
    }

    internal class RequestManager(ClientRequestContext context, ILogger<RequestManager> logger, IResponseSerializer serializer)
        : RequestManagerBase(context, logger, serializer), IRequestManager
    {
    }

    internal class RequestManager<TContext>(ClientRequestContext<TContext> context, ILogger<RequestManager<TContext>> logger, IResponseSerializer serializer)
        : RequestManagerBase(context, logger, serializer), IRequestManager<TContext>
    {
    }
}
