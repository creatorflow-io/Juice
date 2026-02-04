namespace Juice.MediatR
{
    public interface IRequestManagerBase
    {
        ValueTask<bool> TryCreateRequestForCommandAsync<T>(Guid id)
            where T : IBaseRequest;

        ValueTask TryCompleteRequestAsync<T>(Guid id, bool success, object? result = default)
            where T : IBaseRequest;

        ValueTask<TR?> GetCachedResultAsync<T, TR>(Guid id)
            where T : IBaseRequest;
    }

    public interface IRequestManager : IRequestManagerBase
    {

    }

    public interface IRequestManager<TContext> : IRequestManagerBase
    {

    }
}
