#if NET8_0_OR_GREATER
using Juice.Messaging;
using Juice.Messaging.Idempotency;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Juice.AspNetCore.Idempotency
{
    /// <summary>
    /// Minimal-API equivalent of <see cref="IdempotencyKeyActionFilter"/> (net8+). Attach to endpoints
    /// carrying <see cref="IdempotentAttribute"/> metadata. Capture is best-effort for value-returning
    /// handlers; handlers returning an opaque <see cref="IResult"/> are replayed as an empty success.
    /// </summary>
    public sealed class IdempotencyEndpointFilter : IEndpointFilter
    {
        public const string HeaderName = "Idempotency-Key";

        private readonly IIdempotencyService _service;
        private readonly IdempotencyScopeResolver _scopeResolver;
        private readonly IMessageSerializer _serializer;
        private readonly IdempotencyOptions _options;

        public IdempotencyEndpointFilter(
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

        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            var http = context.HttpContext;
            var attribute = http.GetEndpoint()?.Metadata.GetMetadata<IdempotentAttribute>();
            if (attribute is null)
            {
                return await next(context);
            }

            if (!http.Request.Headers.TryGetValue(HeaderName, out var values) || StringValues.IsNullOrEmpty(values))
            {
                return IdempotencyProblems.HeaderRequired(HeaderName).AsResult();
            }

            var key = values.ToString();
            if (string.IsNullOrWhiteSpace(key))
            {
                return IdempotencyProblems.InvalidKey("The Idempotency-Key must not be empty.").AsResult();
            }
            if (key.Length > _options.MaxKeyLength)
            {
                return IdempotencyProblems
                    .InvalidKey($"The Idempotency-Key must be at most {_options.MaxKeyLength} characters.")
                    .AsResult();
            }

            var scope = _scopeResolver.Resolve(http, attribute.Scope);
            var normalizedBody = RequestFingerprint.SerializePayload(context.Arguments, _serializer);
            var requestHash = RequestFingerprint.Compute(http.Request.Method, http.Request.Path, normalizedBody);

            var begin = await _service.TryBeginRequestAsync(scope, key, requestHash, http.RequestAborted);
            switch (begin.Outcome)
            {
                case IdempotencyOutcome.Conflict:
                    return IdempotencyProblems.Conflict().AsResult();
                case IdempotencyOutcome.InProgress:
                    http.Response.Headers.RetryAfter = "1";
                    return IdempotencyProblems.InProgress().AsResult();
                case IdempotencyOutcome.Completed:
                    return BuildReplay(begin.StoredResult);
            }

            object? result;
            try
            {
                result = await next(context);
            }
            catch
            {
                await _service.TryCompleteRequestAsync(scope, key, false, null, http.RequestAborted);
                throw;
            }

            var envelope = CaptureEnvelope(result);
            var success = envelope is null || envelope.StatusCode < 500;
            await _service.TryCompleteRequestAsync(scope, key, success, success ? envelope : null, http.RequestAborted);
            return result;
        }

        private ResponseEnvelope? CaptureEnvelope(object? result)
        {
            if (result is null || result is IResult)
            {
                // Opaque IResult bodies cannot be introspected without executing them.
                return null;
            }
            return new ResponseEnvelope
            {
                StatusCode = StatusCodes.Status200OK,
                ContentType = "application/json",
                // System.Text.Json to match minimal-API's own response serialization for faithful replay.
                Body = System.Text.Json.JsonSerializer.Serialize(result)
            };
        }

        private IResult BuildReplay(string? stored)
        {
            if (string.IsNullOrEmpty(stored))
            {
                return Results.StatusCode(StatusCodes.Status200OK);
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
                return Results.StatusCode(StatusCodes.Status200OK);
            }

            return Results.Content(envelope.Body ?? string.Empty,
                envelope.ContentType ?? "application/json",
                statusCode: envelope.StatusCode);
        }
    }
}
#endif
