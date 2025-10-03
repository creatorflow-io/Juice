namespace Juice.MediatR
{
    public interface IBaseRequest
    {

    }
    /// <summary>
    /// Marker interface for a request with a void response.
    /// </summary>
    public interface IRequest : IBaseRequest
    {
    }
    /// <summary>
    /// Marker interface for a request with a response of type <typeparamref name="TResponse"/>.
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    public interface IRequest<out TResponse> : IBaseRequest
    {
    }
}
