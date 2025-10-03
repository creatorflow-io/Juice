namespace Juice.MediatR
{
    /// <summary>
    /// Identified request with a unique ID to track duplicate requests for the same operation.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IIdentifiedRequest<T>
        where T : IBaseRequest
    {
        Guid Id { get; }
        T Command { get; }
    }
}
