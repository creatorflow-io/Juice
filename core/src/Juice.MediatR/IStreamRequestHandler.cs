namespace Juice.MediatR
{
    public interface IStreamRequestHandler<in TRequest, TResponse>
        where TRequest : IStreamRequest<TResponse>
    {
        IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
    }
}
