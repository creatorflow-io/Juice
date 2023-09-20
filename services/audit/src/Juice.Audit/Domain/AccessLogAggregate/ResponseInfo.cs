using Juice.Domain;

namespace Juice.Audit.Domain.AccessLogAggregate
{
    public class ResponseInfo
    {
        public int Status { get; private set; }
        /// <summary>
        /// Intended to be used for storing the response data and will fullfilled by custom middleware.
        /// </summary>
        public string? Data { get; private set; }
        /// <summary>
        /// Intended to be used for storing the response message and will fullfilled by custom middleware.
        /// </summary>
        public string? Msg { get; private set; }
        /// <summary>
        /// Intended to be used for storing the response error and will fullfilled by custom middleware.
        /// </summary>
        public string? Err { get; private set; }
        public string? Headers { get; private set; }

        public long? ElapsedMs { get; private set; }

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
            Msg ??= ValidatableExtensions.TrimExceededLength(message, LengthConstants.ShortDescriptionLength);
        }
        /// <summary>
        /// Intended to be used for storing the response error and will fullfilled by custom middleware.
        /// </summary>
        /// <param name="error"></param>
        public void TrySetError(string error)
        {
            Err ??= error;
        }

        public void SetResponseInfo(int statusCode, string headers, long elapsedMilliseconds)
        {
            Status = statusCode;
            Headers = ValidatableExtensions.TrimExceededLength(headers, LengthConstants.ShortDescriptionLength);
            ElapsedMs = elapsedMilliseconds;
        }
    }
}
