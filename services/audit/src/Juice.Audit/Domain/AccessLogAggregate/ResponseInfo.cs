namespace Juice.Audit.Domain.AccessLogAggregate
{
    public class ResponseInfo
    {
        public int StatusCode { get; private set; }
        /// <summary>
        /// Intended to be used for storing the response data and will fullfilled by custom middleware.
        /// </summary>
        public string? Data { get; private set; }
        /// <summary>
        /// Intended to be used for storing the response message and will fullfilled by custom middleware.
        /// </summary>
        public string? Message { get; private set; }
        /// <summary>
        /// Intended to be used for storing the response error and will fullfilled by custom middleware.
        /// </summary>
        public string? Error { get; private set; }
        public string? Headers { get; private set; }

        public long? ElapsedMilliseconds { get; private set; }

        /// <summary>
        /// Intended to be used for storing the response data and will fullfilled by custom middleware.
        /// </summary>
        /// <param name="data"></param>
        public void TrySetData(string data)
        {
            Data ??= data;
        }
        /// <summary>
        /// Intended to be used for storing the response message and will fullfilled by custom middleware.
        /// </summary>
        /// <param name="message"></param>
        public void TrySetMessage(string message)
        {
            Message ??= message;
        }
        /// <summary>
        /// Intended to be used for storing the response error and will fullfilled by custom middleware.
        /// </summary>
        /// <param name="error"></param>
        public void TrySetError(string error)
        {
            Error ??= error;
        }

        public void SetResponseInfo(int statusCode, string headers, long elapsedMilliseconds)
        {
            StatusCode = statusCode;
            Headers = headers;
            ElapsedMilliseconds = elapsedMilliseconds;
        }
    }
}
