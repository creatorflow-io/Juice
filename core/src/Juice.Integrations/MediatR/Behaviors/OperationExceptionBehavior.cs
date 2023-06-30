using Juice.EventBus;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Juice.Integrations.MediatR.Behaviors
{
    internal class OperationExceptionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
        where TResponse : IOperationResult
    {
        private readonly ILogger _logger;
        public OperationExceptionBehavior(ILogger<OperationExceptionBehavior<TRequest, TResponse>> logger)
        {
            _logger = logger;
        }
        public async Task<TResponse?> Handle(TRequest request, CancellationToken cancellationToken, RequestHandlerDelegate<TResponse> next)
        {
            var typeName = request.GetGenericTypeName();
            try
            {
                var result = await next();

                if (result != null && !result.Succeeded)
                {
                    _logger.LogError("Command {typeName} {request} not success. {message}", typeName, request?.ToString() ?? "", result.Message);
                    if (_logger.IsEnabled(LogLevel.Debug) && result is OperationResult rs1 && rs1.Exception != null)
                    {
                        _logger.LogDebug(rs1.Exception.StackTrace);
                    }
                }
                else if (result == null)
                {
                    _logger.LogError("Command {typeName} {request} return invalid result", typeName, request?.ToString() ?? "");
                }
                else if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Command {typeName} {request} return Succeeded state", typeName, request?.ToString() ?? "");
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError("ERROR Handling command {typeName} {request}. {message}", typeName, request?.ToString() ?? "", ex.Message);
                _logger.LogTrace("ERROR Handling command {typeName}. Trace: {trace}", typeName, ex.StackTrace);
                return OperationResult.Failed(ex, $"Failed to handle command {typeName}. {ex.Message}") is TResponse response ? response : default;
            }
        }
    }
}
