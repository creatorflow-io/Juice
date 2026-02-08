using Juice.Messaging;
using Juice.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.Middlewares
{
    public class MessageContextMiddleware
    {
        private const string CorrelationHeader = "x-correlation-id";

        private readonly RequestDelegate _next;
        private readonly string _source;

        public MessageContextMiddleware(
            RequestDelegate next,
            string source)
        {
            _next = next;
            _source = source;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            var correlationId =
                httpContext.Request.Headers[CorrelationHeader]
                    .FirstOrDefault()
                ?? StringIdGenerator.Instance.GenerateUniqueId();

            // INIT context (ENTRY POINT)
            MessageContext.Initialize(
                correlationId: correlationId,
                causationId: null,
                executionId: StringIdGenerator.Instance.GenerateUniqueId(),
                source: _source
            );

            // Optional: propagate back to response
            httpContext.Response.Headers[CorrelationHeader] = correlationId;
            var logger = httpContext.RequestServices.GetRequiredService<ILogger<MessageContextMiddleware>>();
            try
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug(
                    "HTTP request started {@ctx}",
                    MessageLogContext.ToLogObject(
                        action: "http.request.start")
                    );
                }
                
                await _next(httpContext);
            }
            finally
            {
                // CRITICAL: avoid context leak
                MessageContext.Clear();
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug(
                    "HTTP request finished {@ctx}",
                    MessageLogContext.ToLogObject(
                        action: "http.request.finish")
                    );
                }
            }
        }
    }
}
