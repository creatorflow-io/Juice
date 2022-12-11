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
        public OperationExceptionBehavior(ILogger<TRequest> logger)
        {
            _logger = logger;
        }
        public async Task<TResponse?> Handle(TRequest request, CancellationToken cancellationToken, RequestHandlerDelegate<TResponse> next)
        {
            var typeName = request.GetGenericTypeName();
            try
            {
                return await next();
            }
            catch (Exception ex)
            {
                _logger.LogError("ERROR Handling command {typeName} {request}. {message}", typeName, request?.ToString() ?? "", ex.Message);
                _logger.LogTrace(ex, "ERROR Handling command {typeName}.", typeName);
                return OperationResult.Failed(ex, $"Failed to handle command {typeName}") is TResponse response ? response : default;
            }
        }
    }
}
