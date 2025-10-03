namespace Juice.MediatR
{
    public interface IStreamPipelineBehavior<TRequest, TResponse> : IPipelineBehavior
    where TRequest : IStreamRequest<TResponse>
    {
        IAsyncEnumerable<TResponse> Handle(
            TRequest request,
            StreamHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken);
    }

}
