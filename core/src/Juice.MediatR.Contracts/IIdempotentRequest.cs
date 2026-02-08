namespace Juice.MediatR
{
    /// <summary>
    /// Identified request with a unique ID to track duplicate requests for the same operation.
    /// </summary>
    public interface IIdempotentRequest: IBaseRequest
    {
        string IdempotencyKey { get; }
    }
}
