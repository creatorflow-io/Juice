using System.Runtime.CompilerServices;
namespace Juice.MediatR
{
    public readonly struct StreamHandlerDelegate<TRequest, TResponse>
    {
        private readonly Func<TRequest, CancellationToken, IAsyncEnumerable<TResponse>> _next;
        internal StreamHandlerDelegate(Func<TRequest, CancellationToken, IAsyncEnumerable<TResponse>> next) => _next = next;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IAsyncEnumerable<TResponse> Invoke(TRequest request, CancellationToken ct) => _next(request, ct);
    }
}
