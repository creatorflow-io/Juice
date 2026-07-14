using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MvcProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

namespace Juice.AspNetCore.Idempotency
{
    /// <summary>
    /// RFC 7807 ProblemDetails factory for the idempotency rejection cases:
    /// header-required (400), invalid-key (400), in-progress (409), conflict (422).
    /// </summary>
    public static class IdempotencyProblems
    {
        public const int StatusUnprocessableEntity = 422;

        public static MvcProblemDetails HeaderRequired(string headerName = "Idempotency-Key") => new()
        {
            Status = StatusCodes.Status400BadRequest,
            Title = $"{headerName} header required",
            Detail = $"This endpoint requires an '{headerName}' request header."
        };

        public static MvcProblemDetails InvalidKey(string reason) => new()
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid Idempotency-Key",
            Detail = reason
        };

        public static MvcProblemDetails InProgress() => new()
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Request in progress",
            Detail = "A request with this Idempotency-Key is currently being processed. Retry shortly."
        };

        public static MvcProblemDetails Conflict() => new()
        {
            Status = StatusUnprocessableEntity,
            Title = "Idempotency key conflict",
            Detail = "This Idempotency-Key was already used with a different request payload."
        };

        /// <summary>Wrap a ProblemDetails as an MVC action result with the problem+json content type.</summary>
        public static IActionResult AsActionResult(this MvcProblemDetails problem) =>
            new ObjectResult(problem)
            {
                StatusCode = problem.Status,
                ContentTypes = { "application/problem+json" }
            };

        /// <summary>Wrap a ProblemDetails as a minimal-API result.</summary>
        public static IResult AsResult(this MvcProblemDetails problem) =>
            Results.Problem(
                statusCode: problem.Status,
                title: problem.Title,
                detail: problem.Detail);
    }
}
