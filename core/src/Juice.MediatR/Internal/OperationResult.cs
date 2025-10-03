using Juice.Operation;

namespace Juice.MediatR.Internal
{
    internal class OperationResult : IOperationResult
    {
        public string? Message { get; set; }

        public string? StackTrace => throw new NotImplementedException();

        public bool Succeeded { get; set; }

        public OperationalFailure Failure => throw new NotImplementedException();

        public Exception? Exception => throw new NotImplementedException();

        public void ThrowIfNotSucceeded() => throw new NotImplementedException();

        public override string ToString() => base.ToString() ?? "";
    }

    internal class OperationResult<T> : OperationResult, IOperationResult<T>
    {
        public T? Data { get; set; }
    }
}
