namespace Juice.MediatR
{
    public interface IRequestSender
    {
        ValueTask Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest;
        ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
        IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default);
    }
}
