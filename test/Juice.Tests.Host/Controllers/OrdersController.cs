using Juice.AspNetCore.Idempotency;
using Microsoft.AspNetCore.Mvc;

namespace Juice.Tests.Host.Controllers
{
    /// <summary>
    /// Real state-changing endpoint backing juice-layout's "Real backend" button
    /// (<c>environment.idempotencyDemoApi</c> → <c>POST /api/orders</c>). Unlike the
    /// deterministic <c>/mock-api/*</c> emulators, this goes through the actual Juice
    /// HTTP idempotency layer: the global <c>IdempotencyKeyActionFilter</c> (installed by
    /// <c>AddApiIdempotency</c>) enforces the <c>Idempotency-Key</c> header against the
    /// configured <see cref="Juice.Messaging.Idempotency.IIdempotencyService"/> store
    /// (EF, per the module's <c>AddIdempotencyEF</c>).
    /// </summary>
    [ApiController]
    public sealed class OrdersController : ControllerBase
    {
        /// <summary>
        /// Creates an order. The filter makes this exactly-once per <c>(scope, key)</c>:
        /// a repeat with the same key replays the stored response (same <c>orderId</c>),
        /// the same key with a different payload returns 422, and a missing key returns 400.
        /// </summary>
        /// <param name="request">The order payload <c>{ amount, at }</c>.</param>
        /// <param name="delayMs">
        /// Test-only: artificial in-flight delay (ms) held INSIDE the idempotency window
        /// (the filter has already marked the record InProgress by the time the action runs).
        /// Widens the window so a concurrent burst on one key deterministically hits the
        /// server's InProgress branch (<c>409</c> + <c>Retry-After</c>) instead of racing
        /// to completion first. Default 0 → instant, as before.
        /// </param>
        [HttpPost("/api/orders")]
        [Idempotent(Scope = "orders")]
        public async Task<IActionResult> CreateAsync([FromBody] OrderRequest request, [FromQuery] int delayMs = 0)
        {
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, HttpContext.RequestAborted);
            }

            var key = Request.Headers["Idempotency-Key"].ToString();
            return Ok(new
            {
                ok = true,
                orderId = Guid.NewGuid().ToString("N"),
                amount = request.Amount,
                at = request.At,
                key,
                replayed = false
            });
        }
    }

    /// <summary>Payload sent by the demo: <c>{ amount, at }</c>.</summary>
    public sealed record OrderRequest(decimal Amount, int At);
}
