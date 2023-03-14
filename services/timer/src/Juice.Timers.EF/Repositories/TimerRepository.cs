using Juice.Timers.Domain.AggregratesModel.TimerAggregrate;
using Microsoft.EntityFrameworkCore;

namespace Juice.Timers.EF.Repositories
{
    internal class TimerRepository : ITimerRepository
    {
        private readonly TimerDbContext _dbContext;

        public TimerRepository(TimerDbContext context)
        {
            _dbContext = context;
        }

        public async Task<Guid> CreateAsync(TimerRequest request, CancellationToken token)
        {
            var entry = _dbContext.Add(request);
            await _dbContext.SaveChangesAsync();
            return entry.Entity.Id;
        }

        public Task<TimerRequest?> GetAsync(Guid id, CancellationToken token)
            => _dbContext.TimerRequests.FirstOrDefaultAsync(t => t.Id == id, token);

        public Task<TimerRequest?> GetByCorrelationIdAsync(string id, CancellationToken token)
            => _dbContext.TimerRequests.FirstOrDefaultAsync(t => t.CorrelationId == id, token);

        public async Task RemoveTimersBeforeAsync(DateTimeOffset dateTime, CancellationToken token)
        {
            await _dbContext.TimerRequests.Where(x => x.IsCompleted && x.AbsoluteExpired < dateTime)
                .ExecuteDeleteAsync(token);
        }

        public async Task UpdateAsync(TimerRequest request, CancellationToken token)
        {
            _dbContext.Update(request);
            await _dbContext.SaveChangesAsync(token);
        }
    }
}
