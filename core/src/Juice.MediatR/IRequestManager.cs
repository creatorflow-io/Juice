using MediatR;

namespace Juice.MediatR
{
    public interface IRequestManager
    {
        Task<bool> TryCreateRequestForCommandAsync<T, R>(Guid id)
            where T : IRequest<R>;

        Task TryCompleteRequestAsync(Guid id, bool success);
    }
}
