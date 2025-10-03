using Microsoft.EntityFrameworkCore;

namespace Juice.MediatR.RequestManager.EF
{
    internal class RequestManagerBase : IRequestManagerBase
    {
        private ClientRequestContextBase _context;
        public RequestManagerBase(ClientRequestContextBase context)
        {
            _context = context;
        }

        public async ValueTask TryCompleteRequestAsync<T>(Guid id, bool success)
            where T : IBaseRequest
        {
            try
            {
                var request = await _context.ClientRequests.FindAsync(id, typeof(T).Name);
                if (request != null)
                {
                    if (success)
                    {
                        request.MarkAsDone();
                    }
                    else
                    {
                        request.MarkAsFailed();
                    }
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception)
            {

            }
        }

        public async ValueTask<bool> TryCreateRequestForCommandAsync<T>(Guid id)
            where T : IBaseRequest
        {
            // retry failed or interupted conmmands
            if (await _context.ClientRequests.AnyAsync(r => r.Id == id
                && r.Name == typeof(T).Name
                && (r.State == RequestState.ProcessedFailed
                || (r.State == RequestState.New && r.Time < DateTimeOffset.Now.AddSeconds(-15)))))
            {
                return true;
            }
            if (await _context.ClientRequests.AnyAsync(r => r.Id == id && r.Name == typeof(T).Name))
            {
                return false;
            }
            try
            {
                _context.ClientRequests.Add(new ClientRequest(id, typeof(T).Name));
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException)
            {
                return false;
            }
        }
    }

    internal class RequestManager : RequestManagerBase, IRequestManager
    {
        public RequestManager(ClientRequestContext context) : base(context)
        {
        }
    }

    internal class RequestManager<TContext> : RequestManagerBase, IRequestManager<TContext>
    {
        public RequestManager(ClientRequestContext<TContext> context) : base(context)
        {
        }
    }
}
