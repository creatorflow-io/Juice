using Juice.Messaging;
using Juice.Services;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Juice.AspNetCore.Mvc.Filters
{
    /// <summary>
    /// Initializes <see cref="MessageContext"/> for the decorated controller or action.
    /// Reads the <c>x-correlation-id</c> header from the request (or generates one),
    /// sets the <see cref="Source"/> as the context source, and clears the context
    /// after the action completes.
    /// <para>
    /// Use this attribute when <see cref="Juice.Middlewares.MessageContextMiddleware"/>
    /// is not registered globally, or when you need a different <see cref="Source"/>
    /// per controller or action.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [MessageContext(Source = "orders-api")]
    /// public class OrdersController : ControllerBase { ... }
    ///
    /// // Or per-action:
    /// [MessageContext(Source = "webhook-handler")]
    /// [HttpPost("webhook")]
    /// public async Task&lt;IActionResult&gt; HandleWebhookAsync() { ... }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class MessageContextAttribute : ActionFilterAttribute
    {
        private const string CorrelationHeader = "x-correlation-id";

        /// <summary>
        /// The source identifier written into <see cref="MessageContext"/>.
        /// Defaults to <c>"http"</c>.
        /// </summary>
        public string Source { get; set; } = "http";

        public override async Task OnActionExecutionAsync(
            ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (MessageContext.IsInitialized)
            {
                // Already initialized (e.g. by MessageContextMiddleware) — do not overwrite.
                await next();
                return;
            }

            var correlationId =
                context.HttpContext.Request.Headers[CorrelationHeader]
                    .FirstOrDefault()
                ?? StringIdGenerator.Instance.GenerateUniqueId();

            MessageContext.Initialize(
                correlationId: correlationId,
                causationId: null,
                executionId: StringIdGenerator.Instance.GenerateUniqueId(),
                source: Source);

            context.HttpContext.Response.Headers[CorrelationHeader] = correlationId;

            try
            {
                await next();
            }
            finally
            {
                MessageContext.Clear();
            }
        }
    }
}
