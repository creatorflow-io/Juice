using Microsoft.EntityFrameworkCore;

namespace Juice.MediatR.IdentifiedCommands.EF
{
    public class RequestManager : IRequestManager
    {
        private ClientRequestContext _context;
        public RequestManager(ClientRequestContext context)
        {
            _context = context;
        }

        public async Task CompleteRequestAsync(Guid id, bool success)
        {
            var request = await _context.ClientRequests.FindAsync(id);
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

        public async Task<bool> CreateRequestForCommandAsync<T>(Guid id)
        {
            if (await _context.ClientRequests.AnyAsync(r => r.Id == id))
            {
                return false;
            }
            _context.ClientRequests.Add(new ClientRequest(id, typeof(T).Name));
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
