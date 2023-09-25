using Juice.Extensions;
using Microsoft.Extensions.Logging;

namespace Juice.BgService.Extensions
{
    public static class LoggerExtensions
    {
        #region Service
        private static readonly Action<ILogger, string, Exception?> _failedToInvoke;
        private static readonly Action<ILogger, string, string, string, Exception?> _serviceChanged;
        private static readonly Action<ILogger, string, string, Exception?> _serviceMessage;
        private static readonly Action<ILogger, string, string, Exception?> _serviceFailured;
        #endregion
        #region File watcher
        private static readonly Action<ILogger, string, string, Exception?> _fileChanged;
        private static readonly Action<ILogger, string, string, Exception?> _fileRenamed;
        private static readonly Action<ILogger, string, string, Exception?> _fileProcessed;
        private static readonly Action<ILogger, string, string, string, Exception?> _fileProcessedFailure;
        #endregion
        static LoggerExtensions()
        {
            #region Service
            _failedToInvoke = LoggerMessage.Define<string>(
                LogLevel.Error,
                LogEvents.ServiceInvokeFailed,
                "An error occurred while processing Invoke method. {Message}");

            _serviceChanged = LoggerMessage.Define<string, string, string>(LogLevel.Information,
                default,
                "Service {Name} was changed to {State} state. {Message}"
                );

            _serviceMessage = LoggerMessage.Define<string, string>(LogLevel.Information,
                default,
                "Service {Name} has new message: {Message}"
                );

            _serviceFailured = LoggerMessage.Define<string, string>(LogLevel.Critical,
                default,
                "Service {Name} has stopped unexpectedly. {Message}");
            #endregion

            #region File watcher
            _fileChanged = LoggerMessage.Define<string, string>(LogLevel.Information,
                default,
                "File {FilePath} {ChangeType}.");
            _fileRenamed = LoggerMessage.Define<string, string>(LogLevel.Information,
               default,
               "File {OriginFilePath} renamed to {FilePath}.");

            _fileProcessed = LoggerMessage.Define<string, string>(LogLevel.Information,
                default,
                "[{ChangeType}] File {FilePath} processed successfully.");

            _fileProcessedFailure = LoggerMessage.Define<string, string, string>(LogLevel.Error,
                default,
                "[{ChangeType}] Failed to process file {FilePath}. {Reason}");
            #endregion
        }


        #region FailedInvoke
        public static void FailedToInvoke(
            this ILogger logger, string? message, Exception? exception) =>
            _failedToInvoke(logger, message ?? "", exception);

        public static void ServiceChanged(
           this ILogger logger, ServiceEventArgs args)
        {
            if (args.EventName == "State")
            {
                if (args.State.IsInFailureStates())
                {
                    _serviceFailured(logger, args.ServiceName, args.Message ?? "", default);
                }
                else
                {
                    _serviceChanged(logger, args.ServiceName, args.State.DisplayValue(), args.Message ?? "", default);
                }
            }
            else if (args.EventName == "Message" && !string.IsNullOrEmpty(args.Message))
            {
                _serviceMessage(logger, args.ServiceName, args.Message, default);
            }
        }
        #endregion

        #region File watcher
        public static void FileChanged(
           this ILogger logger, string filePath, string changeType) =>
           _fileChanged(logger, filePath, changeType, default!);

        public static void FileRenamed(
           this ILogger logger, string originFilePath, string filePath) =>
           _fileRenamed(logger, originFilePath, filePath, default!);

        public static void FileProcessed(
           this ILogger logger, string changeType, string filePath) =>
           _fileProcessed(logger, changeType, filePath, default!);

        public static void FileProcessedFailure(
          this ILogger logger, string changeType, string filePath, Exception? exception) =>
          _fileProcessedFailure(logger, changeType, filePath, exception?.Message ?? "", exception);
        #endregion
    }
}
