using Juice.Messaging;
using Juice.Messaging.Idempotency;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Juice.AspNetCore.Idempotency
{
    /// <summary>
    /// MVC action filter enforcing HTTP idempotency for actions/controllers marked <see cref="IdempotentAttribute"/>.
    /// Reads and validates the <c>Idempotency-Key</c> header, begins the request via
    /// <see cref="IIdempotencyService.TryBeginRequestAsync"/>, and maps the outcome to
    /// replay / 409 / 422 / execute-and-capture.
    /// </summary>
    public sealed class IdempotencyKeyActionFilter : IAsyncActionFilter
    {
        public const string HeaderName = "Idempotency-Key";

        private readonly IIdempotencyService _service;
        private readonly IdempotencyScopeResolver _scopeResolver;
        private readonly IMessageSerializer _serializer;
        private readonly IdempotencyOptions _options;

        public IdempotencyKeyActionFilter(
            IIdempotencyService service,
            IdempotencyScopeResolver scopeResolver,
            IMessageSerializer serializer,
            IOptions<IdempotencyOptions>? options = null)
        {
            _service = service;
            _scopeResolver = scopeResolver;
            _serializer = serializer;
            _options = options?.Value ?? new IdempotencyOptions();
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var attribute = context.ActionDescriptor.EndpointMetadata.OfType<IdempotentAttribute>().FirstOrDefault();
            if (attribute is null)
            {
                // Opt-in: unmarked endpoints incur no idempotency overhead.
                await next();
                return;
            }

            var http = context.HttpContext;

            // FR-001: required header.
            if (!http.Request.Headers.TryGetValue(HeaderName, out var values) || StringValues.IsNullOrEmpty(values))
            {
                context.Result = IdempotencyProblems.HeaderRequired(HeaderName).AsActionResult();
                return;
            }

            var key = values.ToString();

            // FR-006: key validation.
            if (string.IsNullOrWhiteSpace(key))
            {
                context.Result = IdempotencyProblems.InvalidKey("The Idempotency-Key must not be empty.").AsActionResult();
                return;
            }
            if (key.Length > _options.MaxKeyLength)
            {
                context.Result = IdempotencyProblems
                    .InvalidKey($"The Idempotency-Key must be at most {_options.MaxKeyLength} characters.")
                    .AsActionResult();
                return;
            }

            var scope = _scopeResolver.Resolve(http, attribute.Scope);
            var normalizedBody = RequestFingerprint.SerializePayload(context.ActionArguments.Values, _serializer);
            var requestHash = RequestFingerprint.Compute(http.Request.Method, http.Request.Path, normalizedBody);

            var begin = await _service.TryBeginRequestAsync(scope, key, requestHash, http.RequestAborted);
            switch (begin.Outcome)
            {
                case IdempotencyOutcome.Conflict: // FR-005
                    context.Result = IdempotencyProblems.Conflict().AsActionResult();
                    return;

                case IdempotencyOutcome.InProgress: // FR-004
                    http.Response.Headers.RetryAfter = "1";
                    context.Result = IdempotencyProblems.InProgress().AsActionResult();
                    return;

                case IdempotencyOutcome.Completed: // FR-003 replay
                    context.Result = BuildReplay(begin.StoredResult);
                    return;
            }

            // Created: execute the action, capture its response, then complete.
            var executed = await next();

            if (executed.Exception is not null && !executed.ExceptionHandled)
            {
                // Not applied — leave the key retryable (FR-008) and let the exception propagate.
                await _service.TryCompleteRequestAsync(scope, key, false, null, http.RequestAborted);
                return;
            }

            var envelope = CaptureEnvelope(executed.Result);
            // Treat 5xx as not-applied so a retry can re-execute; 2xx/4xx are recorded and replayable.
            var success = envelope is null || envelope.StatusCode < 500;
            await _service.TryCompleteRequestAsync(scope, key, success, success ? envelope : null, http.RequestAborted);
        }

        private ResponseEnvelope? CaptureEnvelope(IActionResult? result)
        {
            switch (result)
            {
                case ObjectResult obj:
                    return new ResponseEnvelope
                    {
                        StatusCode = obj.StatusCode ?? StatusCodes.Status200OK,
                        ContentType = "application/json",
                        // System.Text.Json to match the framework's own response serialization for faithful replay.
                        Body = obj.Value is null ? null : System.Text.Json.JsonSerializer.Serialize(obj.Value)
                    };
                case ContentResult content:
                    return new ResponseEnvelope
                    {
                        StatusCode = content.StatusCode ?? StatusCodes.Status200OK,
                        ContentType = content.ContentType ?? "text/plain",
                        Body = content.Content
                    };
                case StatusCodeResult status:
                    return new ResponseEnvelope { StatusCode = status.StatusCode };
                default:
                    return null;
            }
        }

        private IActionResult BuildReplay(string? stored)
        {
            if (string.IsNullOrEmpty(stored))
            {
                return new StatusCodeResult(StatusCodes.Status200OK);
            }

            ResponseEnvelope? envelope;
            try
            {
                envelope = _serializer.Deserialize<ResponseEnvelope>(stored, typeof(ResponseEnvelope));
            }
            catch
            {
                envelope = null;
            }

            if (envelope is null)
            {
                return new StatusCodeResult(StatusCodes.Status200OK);
            }

            return new ContentResult
            {
                StatusCode = envelope.StatusCode,
                Content = envelope.Body,
                ContentType = envelope.ContentType ?? "application/json"
            };
        }
    }
}
