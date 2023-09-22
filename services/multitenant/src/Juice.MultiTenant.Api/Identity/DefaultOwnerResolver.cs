using System.Security.Claims;

namespace Juice.MultiTenant.Api.Identity
{
    internal class DefaultOwnerResolver : IOwnerResolver
    {
        public Task<string?> GetOwnerAsync(ClaimsPrincipal principal)
            => Task.FromResult(principal.FindFirstValue(ClaimTypes.NameIdentifier));
        public Task<string?> GetOwnerAsync(string userInfo) => Task.FromResult((string?)userInfo);
        public Task<string?> GetOwnerNameAsync(string owner) => Task.FromResult((string?)owner);
    }
}
