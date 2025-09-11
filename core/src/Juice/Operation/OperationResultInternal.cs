using System.Diagnostics;
using Newtonsoft.Json;

namespace Juice.Operation
{
    internal class OperationResultInternal : IOperationResult
    {
        protected string? _message;
        public string? Message
        {
            get { return _message ?? Exception?.InnerException?.Message ?? Exception?.Message; }
            set
            {
                _message = value;
            }
        }
        public string? StackTrace => _trace;
        public bool Succeeded { get; init; }
        public OperationalFailure Failure { get; init; }

        [JsonIgnore]
        private Exception? _exception;
        public Exception? Exception
        {
            get { return _exception; }
            init
            {
                _exception = value;
                if (_exception == null) { return; }
                if (Failure == OperationalFailure.None)
                {
                    Failure = value is UnauthorizedAccessException ? OperationalFailure.Unauthorized
                    : value is NotImplementedException ? OperationalFailure.NotImplemented
                    : value is ArgumentNullException ? OperationalFailure.InvalidArgument
                    : Failure;
                }
                _message ??= _exception.Message;
                SetTraceInfo();
            }
        }

        public OperationResultInternal()
        {
        }

        public OperationResultInternal(OperationalFailure failure, string message)
        {
            Succeeded = false;
            Failure = failure;
            Message = message;
            if (failure != OperationalFailure.None)
            {
                SetTraceInfo();
            }
        }

        protected virtual int StackTraceSkip => 3;
        private string? _trace = default;
        private void SetTraceInfo()
        {
            if (_exception != null) { _trace = _exception.StackTrace; return; }

            var frames = new StackTrace(StackTraceSkip, true).GetFrames();
            if (frames != null)
            {
                _trace = string.Join(Environment.NewLine, frames.Take(3).Select(f => f.ToString()));
            }

        }

        public void ThrowIfNotSucceeded()
        {
            if (!Succeeded)
            {
                Exception ex = new OperationException(Message, _exception);
                if (_trace != null)
                {
                    ex = System.Runtime.ExceptionServices.ExceptionDispatchInfo.SetRemoteStackTrace(ex, _trace);
                }
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(ex);
            }
        }

        protected virtual string FailureMessage => Failure switch
        {
            OperationalFailure.NotFound => "Not Found",
            OperationalFailure.Unauthorized => "Unauthorized",
            OperationalFailure.NotImplemented => "Not Implemented",
            _ => "Operation Failed"
        };

        public override string ToString()
            => Message ?? (Succeeded ? "Operation Succeeded" : FailureMessage);

    }

}
