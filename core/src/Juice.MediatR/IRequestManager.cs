namespace Juice.MediatR
{
    public interface IRequestManagerBase
    {
        ValueTask<bool> TryCreateRequestForCommandAsync<T>(Guid id)
            where T : IBaseRequest;

        ValueTask TryCompleteRequestAsync<T>(Guid id, bool success)
            where T : IBaseRequest;
    }

    public interface IRequestManager : IRequestManagerBase
    {

    }

    public interface IRequestManager<TContext> : IRequestManagerBase
    {

    }
}
