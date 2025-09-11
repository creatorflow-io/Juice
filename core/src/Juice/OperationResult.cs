using System.Runtime.CompilerServices;
using Juice.Operation;
using Newtonsoft.Json;

namespace Juice
{
    public interface IOperationResult
    {
        string? Message { get; }
        string? StackTrace { get; }
        bool Succeeded { get; }
        OperationalFailure Failure { get; }
        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        Exception? Exception { get; }
        void ThrowIfNotSucceeded();
        string ToString();

        virtual OperationModel OperationModel => new OperationModel(Message, StackTrace, Succeeded, Failure);
    }

    public interface IOperationResult<T> : IOperationResult
    {
        T? Data { get; set; }
        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        T DataValue => Data ?? throw new InvalidOperationException("Data is null");
        bool HasData => Data != null;
        bool SucceededWithData => Succeeded && HasData;

        new virtual OperationModel<T> OperationModel => new OperationModel<T>(Message, StackTrace, Succeeded, Failure, Data);
    }

    public static class OperationResultExtensions
    {
        /// <summary>
        /// Convert an <see cref="IOperationResult"/> to an <see cref="IOperationResult{T}"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="operationResult"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static IOperationResult<T> Of<T>(this IOperationResult operationResult, T? data = default)
            => new OperationResultInternal<T>
            {
                Exception = operationResult.Exception,
                Message = operationResult.Message,
                Succeeded = operationResult.Succeeded,
                Failure = operationResult.Failure,
                Data = data
            };
        /// <summary>
        /// Convert an <see cref="IOperationResult"/> to an <see cref="IOperationResult{T}"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="operationResult"></param>
        /// <param name="prependMessage"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static IOperationResult<T> Of<T>(this IOperationResult operationResult, string prependMessage, T? data = default)
            => new OperationResultInternal<T>
            {
                Exception = operationResult.Exception,
                Message = prependMessage + (operationResult.Message ?? ""),
                Succeeded = operationResult.Succeeded,
                Failure = operationResult.Failure,
                Data = data
            };

        /// <summary>
        /// Determine if an <see cref="IOperationResult"/> is unauthorized
        /// </summary>
        /// <param name="operationResult"></param>
        /// <returns></returns>
        public static bool IsUnauthorized(this IOperationResult operationResult)
            => operationResult.Failure == OperationalFailure.Unauthorized;

        /// <summary>
        /// Determine if an <see cref="IOperationResult"/> is not found
        /// </summary>
        /// <param name="operationResult"></param>
        /// <returns></returns>
        public static bool IsNotFound(this IOperationResult operationResult)
            => operationResult.Failure == OperationalFailure.NotFound;

        /// <summary>
        /// Determine if an <see cref="IOperationResult"/> is not implemented
        /// </summary>
        /// <param name="operationResult"></param>
        /// <returns></returns>
        public static bool IsNotImplemented(this IOperationResult operationResult)
            => operationResult.Failure == OperationalFailure.NotImplemented;

        /// <summary>
        /// Determine if an <see cref="IOperationResult"/> is argument null
        /// </summary>
        /// <param name="operationResult"></param>
        /// <returns></returns>
        public static bool IsInvalidArgument(this IOperationResult operationResult)
            => operationResult.Failure == OperationalFailure.InvalidArgument;
    }

    public class OperationResult
    {
        private static readonly OperationResultInternal _success = new OperationResultInternal { Succeeded = true };

        /// <summary>
        /// Return a succeeded <see cref="IOperationResult"/>
        /// </summary>
        public static IOperationResult Success => _success;

        #region OperationResult

        /// <summary>
        /// Create a succeeded <see cref="IOperationResult"/> with a message
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public static IOperationResult Succeeded(string? message)
            => new OperationResultInternal()
            {
                Succeeded = true,
                Message = message
            };

        /// <summary>
        /// Create a failed <see cref="IOperationResult"/> with an <see cref="System.Exception"/>
        /// <para>Consider using <see cref="Unauthorized"/>, <see cref="NotImplemented"/>, <see cref="ArgumentNull"/>... if possible</para>
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        public static IOperationResult Failed(Exception ex)
            => new OperationResultInternal()
            {
                Succeeded = false,
                Exception = ex
            };

        /// <summary>
        /// Create a failed <see cref="IOperationResult"/> with an <see cref="System.Exception"/> and message
        /// <para>Consider using <see cref="Unauthorized"/>, <see cref="NotImplemented"/>, <see cref="ArgumentNull"/>... if possible</para>
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static IOperationResult Failed(Exception ex, string? message)
            => new OperationResultInternal()
            {
                Succeeded = false,
                Message = message,
                Exception = ex
            };

        /// <summary>
        /// Create a failed <see cref="IOperationResult"/> with message
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public static IOperationResult Failed(string? message)
            => new OperationResultInternal()
            {
                Succeeded = false,
                Message = message
            };

        /// <summary>
        /// Create a not found <see cref="IOperationResult"/> with message
        /// </summary>
        /// <param name="value"></param>
        /// <param name="name"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static IOperationResult NotFound(object? value, string? message = null, [CallerArgumentExpression(nameof(value))] string? name = null)
            => new OperationResultInternal(OperationalFailure.NotFound, message ?? $"The {name ?? "value"} could not be found");

        /// <summary>
        /// Create a not implemented <see cref="IOperationResult"/>
        /// </summary>
        /// <param name="name"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static IOperationResult NotImplemented(string? name = default, string? message = null)
        {
            name ??= (new System.Diagnostics.StackTrace()).GetFrame(1)?.GetMethod()?.Name;
            if (name != null)
            {
                name = $"The \"{name}\" method";
            }
            return new OperationResultInternal(OperationalFailure.NotImplemented, message ??
                    $"{name ?? "Method"} was not implemented");
        }

        /// <summary>
        /// Create an unauthorized <see cref="IOperationResult"/> with message
        /// </summary>
        /// <param name="name"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static IOperationResult Unauthorized(string? name = default, string? message = null)
        {
            name ??= (new System.Diagnostics.StackTrace()).GetFrame(1)?.GetMethod()?.Name;
            if (name != null)
            {
                name = $"\"{name}\"";
            }
            return new OperationResultInternal(OperationalFailure.Unauthorized, message ?? $"User must be authorized to performs {name ?? "the operation"}");
        }

        /// <summary>
        /// Create an argument null <see cref="IOperationResult"/>
        /// </summary>
        /// <param name="argument"></param>
        /// <param name="paramName"></param>
        /// <returns></returns>
        public static IOperationResult ArgumentNull(object? argument, [CallerArgumentExpression("argument")] string? paramName = null)
            => new OperationResultInternal(OperationalFailure.InvalidArgument, $"Argument \"{(paramName ?? "argument").Trim('"')}\" cannot be null");

        /// <summary>
        /// Create an <see cref="IOperationResult"/> from json
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public static IOperationResult? FromJson(string json) => JsonConvert.DeserializeObject<OperationResultInternal>(json);
        #endregion

        #region OperationResult<T>
        /// <summary>
        /// Create a failed <see cref="IOperationResult{T}"/> with an <see cref="System.Exception"/>
        /// <para>Consider using <see cref="Unauthorized"/>, <see cref="NotImplemented"/>, <see cref="ArgumentNull"/>... if possible</para>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ex"></param>
        /// <returns></returns>
        public static IOperationResult<T> Failed<T>(Exception ex)
            => new OperationResultInternal<T>()
            {
                Succeeded = false,
                Exception = ex
            };

        /// <summary>
        /// Create a failed <see cref="IOperationResult{T}"/> with an <see cref="System.Exception"/> and message
        /// <para>Consider using <see cref="Unauthorized"/>, <see cref="NotImplemented"/>, <see cref="ArgumentNull"/>... if possible</para>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ex"></param>
        /// <param name="message"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static IOperationResult<T> Failed<T>(Exception ex, string? message, T? data = default)
            => new OperationResultInternal<T>()
            { Succeeded = false, Exception = ex, Message = message, Data = data };

        /// <summary>
        /// Create a failed <see cref="IOperationResult{T}"/> with message
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="message"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static IOperationResult<T> Failed<T>(string? message, T? data = default)
        {
            if(data is Exception ex)
            {
                return Failed<T>(ex, message);
            }
            return new OperationResultInternal<T>()
            { Succeeded = false, Message = message, Data = data };
        }

        /// <summary>
        /// Create a succeeded <see cref="IOperationResult{T}"/> with data and a message
        /// </summary>
        /// <param name="data"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static IOperationResult<T> Result<T>(T? data, string? message = null)
            => new OperationResultInternal<T>()
            { Succeeded = true, Data = data, Message = message };

        /// <summary>
        /// Create a succeeded <see cref="IOperationResult{T}"/> without data and a message
        /// </summary>
        /// <param name="message"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static IOperationResult<T> Succeeded<T>(string? message = null)
            => new OperationResultInternal<T>()
            { Succeeded = true, Message = message };

        /// <summary>
        /// Create a not found <see cref="IOperationResult"/> with message
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static IOperationResult<T> NotFound<T>(object? value, string? message = null, [CallerArgumentExpression(nameof(value))] string? name = null)
            => new OperationResultInternal<T>(OperationalFailure.NotFound, message ?? $"The {name ?? "value"} could not be found");

        /// <summary>
        /// Create a not implemented <see cref="IOperationResult"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static IOperationResult<T> NotImplemented<T>(string? name = default, string? message = null)
        {
            name ??= (new System.Diagnostics.StackTrace()).GetFrame(1)?.GetMethod()?.Name;
            if (name != null)
            {
                name = $"The \"{name}\" method";
            }
            return new OperationResultInternal<T>(OperationalFailure.NotImplemented, message ??
                    $"{name ?? "Method"} was not implemented");
        }

        public static IOperationResult<T> ArgumentNull<T>(object? argument, [CallerArgumentExpression("argument")] string? paramName = null)
            => new OperationResultInternal<T>(OperationalFailure.InvalidArgument, $"Argument \"{(paramName ?? "argument").Trim('"')}\" cannot be null");

        /// <summary>
        /// Create an <see cref="IOperationResult{T}"/> from json
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="json"></param>
        /// <returns></returns>
        public static IOperationResult<T>? FromJson<T>(string json) => JsonConvert.DeserializeObject<OperationResultInternal<T>>(json);

        #endregion

    }

}
