using Juice.Messaging.Idempotency;
using Microsoft.AspNetCore.Http;

namespace Juice.AspNetCore.Idempotency
{
    /// <summary>
    /// Resolves the idempotency <c>scope</c> for an HTTP request. The endpoint scope (explicit attribute
    /// scope or request path) is composed with the server-resolved tenant by the shared
    /// <see cref="IIdempotencyScopeProvider"/>, so HTTP and MediatR entry points partition keys identically.
    /// </summary>
    public sealed class IdempotencyScopeResolver
    {
        private readonly IIdempotencyScopeProvider _scopeProvider;

        public IdempotencyScopeResolver(IIdempotencyScopeProvider scopeProvider)
        {
            _scopeProvider = scopeProvider;
        }

        /// <summary>
        /// Resolve the tenant-composed scope for an HTTP request: the explicit attribute scope when
        /// provided, otherwise the request path (route-independent, stable per endpoint).
        /// </summary>
        public string Resolve(HttpContext httpContext, string? explicitScope)
        {
            var endpointScope = !string.IsNullOrWhiteSpace(explicitScope)
                ? explicitScope!
                : httpContext.Request.Path.HasValue ? httpContext.Request.Path.Value! : "/";
            return _scopeProvider.Resolve(endpointScope);
        }
    }
}
