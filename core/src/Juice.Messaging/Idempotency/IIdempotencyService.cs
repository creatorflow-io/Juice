namespace Juice.Messaging.Idempotency
{
    public interface IIdempotencyService
    {
        ValueTask<IOperationResult> TryCreateRequestAsync(string scope, string key, CancellationToken cancellationToken = default);

        ValueTask<IOperationResult<TResult>> TryCreateRequestAsync<TResult>(string scope, string key, CancellationToken cancellationToken = default);

        ValueTask TryCompleteRequestAsync(string scope, string key, bool success,
            object? result = default, CancellationToken cancellationToken = default);

    }
}
