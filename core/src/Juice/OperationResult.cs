﻿using Newtonsoft.Json;

namespace Juice
{
    public interface IOperationResult
    {
        public string? Message { get; }
        public bool Succeeded { get; }
    }
    public interface IOperationResult<T> : IOperationResult
    {
        public T? Data { get; set; }
    }

    public class OperationResult : IOperationResult
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
        public bool Succeeded { get; init; }

        [JsonIgnore]
        public Exception? Exception { get; protected set; }

        private static readonly OperationResult _success = new OperationResult { Succeeded = true };
        public static OperationResult Success => _success;

        #region OperationResult
        /// <summary>
        /// Create a failed <see cref="OperationResult"/> with an <see cref="System.Exception"/>
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="errors"></param>
        /// <returns></returns>
        public static OperationResult Failed(Exception? ex)
            => new()
            {
                Succeeded = false,
                Exception = ex
            };

        /// <summary>
        /// Create a failed <see cref="OperationResult"/> with an <see cref="System.Exception"/> and message
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="errors"></param>
        /// <returns></returns>
        public static OperationResult Failed(Exception? ex, string? message)
            => new()
            {
                Succeeded = false,
                Message = message,
                Exception = ex
            };

        /// <summary>
        /// Create a failed <see cref="OperationResult"/> with message
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="errors"></param>
        /// <returns></returns>
        public static OperationResult Failed(string? message)
            => new()
            {
                Succeeded = false,
                Message = message
            };

        #endregion

        #region OperationResult<T>
        /// <summary>
        /// Create a failed <see cref="OperationResult{T}"/> with an <see cref="System.Exception"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ex"></param>
        /// <param name="errors"></param>
        /// <returns></returns>
        public static OperationResult<T> Failed<T>(Exception? ex)
            => new()
            { Succeeded = false, Exception = ex };

        /// <summary>
        /// Create a failed <see cref="OperationResult{T}"/> with an <see cref="System.Exception"/> and message
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ex"></param>
        /// <param name="errors"></param>
        /// <returns></returns>
        public static OperationResult<T> Failed<T>(Exception? ex, string? message)
            => new()
            { Succeeded = false, Exception = ex, Message = message };

        /// <summary>
        /// Create a failed <see cref="OperationResult{T}"/> with message
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ex"></param>
        /// <param name="errors"></param>
        /// <returns></returns>
        public static OperationResult<T> Failed<T>(string? message)
            => new()
            { Succeeded = false, Message = message };

        /// <summary>
        /// Create a succeeded <see cref="OperationResult"/> with <see cref="T"/> data and a message
        /// </summary>
        /// <param name="data"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static OperationResult<T> Result<T>(T data, string? message = null)
            => new()
            { Succeeded = true, Data = data, Message = message };

        public static OperationResult<T> FromJson<T>(string json) => JsonConvert.DeserializeObject<OperationResult<T>>(json);

        #endregion

        public override string? ToString()
            => Message ?? base.ToString();

    }

    public class OperationResult<T> : OperationResult, IOperationResult<T?>
    {
        public T? Data { get; set; }

    }
}
