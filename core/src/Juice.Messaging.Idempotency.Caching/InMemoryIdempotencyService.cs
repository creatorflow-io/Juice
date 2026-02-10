using System.Collections.Concurrent;

namespace Juice.Messaging.Idempotency
{
    internal class InMemoryIdempotencyService : IIdempotencyService
    {
        private readonly ConcurrentDictionary<string, RequestState> _requests = new();
        private readonly ConcurrentDictionary<string, object?> _results = new();

        public ValueTask TryCompleteRequestAsync(string scope, string key, bool success, object? result = null, CancellationToken cancellationToken = default)
        {
            var requestKey = GetKey(scope, key);
            
            if (success)
            {
                // Mark as completed and cache result
                _requests[requestKey] = RequestState.Completed;
                if (result != null)
                {
                    _results[requestKey] = result;
                }
            }
            else
            {
                // Remove failed request to allow retry
                _requests.TryRemove(requestKey, out _);
                _results.TryRemove(requestKey, out _);
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask<IOperationResult> TryCreateRequestAsync(string scope, string key, CancellationToken cancellationToken = default)
        {
            var requestKey = GetKey(scope, key);

            // Try to add the request marker
            if (_requests.TryAdd(requestKey, RequestState.InProgress))
            {
                // Successfully created
                return ValueTask.FromResult(OperationResult.Success);
            }

            // Request already exists - check if it's completed with a cached result
            if (_requests.TryGetValue(requestKey, out var state) && state == RequestState.Completed)
            {
                // Request is completed, return failed with no data
                return ValueTask.FromResult(OperationResult.Failed("Request already processed"));
            }

            // Request is in progress or already exists
            return ValueTask.FromResult(OperationResult.Failed("Request already in progress"));
        }

        public ValueTask<IOperationResult<TResult>> TryCreateRequestAsync<TResult>(string scope, string key, CancellationToken cancellationToken = default)
        {
            var requestKey = GetKey(scope, key);

            // Try to add the request marker
            if (_requests.TryAdd(requestKey, RequestState.InProgress))
            {
                // Successfully created
                return ValueTask.FromResult(OperationResult.Succeeded<TResult>("Request created"));
            }

            // Request already exists - check if it's completed with a cached result
            if (_requests.TryGetValue(requestKey, out var state) && state == RequestState.Completed)
            {
                // Try to get cached result
                if (_results.TryGetValue(requestKey, out var cachedResult))
                {
                    // Return the cached result
                    if (cachedResult is TResult typedResult)
                    {
                        return ValueTask.FromResult(OperationResult.Failed("Cached result", typedResult));
                    }
                    else if (cachedResult != null)
                    {
                        // Try to handle IOperationResult<T> case
                        var resultType = cachedResult.GetType();
                        if (resultType.IsGenericType && 
                            resultType.GetGenericTypeDefinition().GetInterfaces().Any(i => 
                                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IOperationResult<>)))
                        {
                            return ValueTask.FromResult(OperationResult.Failed("Cached result", (TResult)cachedResult));
                        }
                    }
                }

                // Completed but no result or wrong type
                return ValueTask.FromResult(OperationResult.Failed<TResult>("Request already processed"));
            }

            // Request is in progress
            return ValueTask.FromResult(OperationResult.Failed<TResult>("Request already in progress"));
        }

        private static string GetKey(string scope, string key) => $"{scope}:{key}";

        private enum RequestState
        {
            InProgress,
            Completed
        }
    }
}
