namespace Juice
{
    public class OperationResult
    {
        public object? Data { get; set; }
        public string? Message { get; set; }
        public bool Succeeded { get; protected set; }
        private static readonly OperationResult _success = new OperationResult { Succeeded = true };
        public static OperationResult Success => _success;
        public Exception? Exception { get; protected set; }
        public static OperationResult Failed(Exception? ex, string? message = null)
        {
            var result = new OperationResult { Succeeded = false };
            if (ex != null)
            {
                result.Exception = (ex);
            }
            result.Message = message;
            return result;
        }
        public void ThrowIfNotSuccess()
        {
            if (Exception != null)
            {
                throw Exception;
            }
        }

        public override string? ToString()
        {
            return Message ?? Exception?.InnerException?.Message ?? Exception?.Message ?? (Succeeded ? "Succeeded" : base.ToString());
        }

        public OperationResult ToJsonSafetyResult()
        {
            if (Exception != null)
            {
                var message = Exception.InnerException?.Message ?? Exception.Message;
                return Failed(null, message + Exception.StackTrace);
            }
            return this;
        }

        public OperationResult OperationSucceeded(string message = null)
        {
            Succeeded = true;
            if (message != null) { Message = message; }
            return this;
        }
    }
}
