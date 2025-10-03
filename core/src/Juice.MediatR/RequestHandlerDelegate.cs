using System.Runtime.CompilerServices;

namespace Juice.MediatR
{
    public readonly struct RequestHandlerDelegate<TRequest, TResponse>
    {
        private readonly Func<TRequest, CancellationToken, ValueTask<TResponse>> _next;
        internal RequestHandlerDelegate(Func<TRequest, CancellationToken, ValueTask<TResponse>> next) => _next = next;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<TResponse> Invoke(TRequest request, CancellationToken ct) => _next(request, ct);
    }

    public readonly struct RequestHandlerDelegate<TRequest>
        where TRequest : IRequest
    {
        private readonly Func<TRequest, CancellationToken, ValueTask> _next;
        internal RequestHandlerDelegate(Func<TRequest, CancellationToken, ValueTask> next) => _next = next;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask Invoke(TRequest request, CancellationToken ct) => _next(request, ct);
    }
}
