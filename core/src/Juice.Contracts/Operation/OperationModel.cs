namespace Juice.Operation
{
    public record OperationModel
    {
        public string? Message { get; }
        public string? StackTrace { get; }
        public bool Succeeded { get; }
        public OperationalFailure Failure { get; }

        public OperationModel(string? message, string? stackTrace, bool succeeded, OperationalFailure failure)
        {
            Message = message;
            StackTrace = stackTrace;
            Succeeded = succeeded;
            Failure = failure;
        }
    }

    public record OperationModel<T>:OperationModel
    {
        public T? Data { get; }

        public OperationModel(string? message, string? stackTrace, bool succeeded, OperationalFailure failure, T? data) : base(message, stackTrace, succeeded, failure)
        {
            Data = data;
        }
    }
}
