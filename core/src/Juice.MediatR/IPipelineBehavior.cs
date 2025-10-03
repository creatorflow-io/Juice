namespace Juice.MediatR
{
    // Pipeline behavior (around advice)
    public interface IPipelineBehavior
    {
        int Order { get; }
    }
    public interface IPipelineBehavior<TRequest, TResponse>: IPipelineBehavior
    {
        ValueTask<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TRequest, TResponse> next,
            CancellationToken cancellationToken);
    }

    public interface IPipelineBehavior<TRequest> : IPipelineBehavior
        where TRequest : IRequest
    {
        ValueTask Handle(
            TRequest request,
            RequestHandlerDelegate<TRequest> next,
            CancellationToken cancellationToken);
    }
}
