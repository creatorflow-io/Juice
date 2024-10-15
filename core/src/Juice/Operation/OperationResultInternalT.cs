namespace Juice.Operation
{
    internal class OperationResultInternal<T> : OperationResultInternal, IOperationResult<T>
    {
        public OperationResultInternal()
        {
        }
        public OperationResultInternal(OperationalFailure failure, string message) : base(failure, message)
        {
        }

        override protected int StackTraceSkip => 4;
        public T? Data { get; set; }
    }
}
