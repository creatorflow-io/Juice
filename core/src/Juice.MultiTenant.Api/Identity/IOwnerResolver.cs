using System.Security.Claims;

namespace Juice.MultiTenant.Api.Identity
{
    public interface IOwnerResolver
    {
        /// <summary>
        /// Implement this method to resolve the owner info from <see cref="ClaimsPrincipal"/>,
        /// so you can decide to use userid or username for the tenant owner.
        /// </summary>
        /// <param name="principal"></param>
        /// <returns></returns>
        Task<string?> GetOwnerAsync(ClaimsPrincipal principal);

        /// <summary>
        /// Implement this method to resolve the owner info from userInfo,
        /// so you can use custom logic to resolve userid or username as the owner info.
        /// You can also use this method to resolve the owner info from other sources.
        /// </summary>
        /// <param name="principal"></param>
        /// <returns></returns>
        Task<string?> GetOwnerAsync(string userInfo);

        /// <summary>
        /// Implement this method to resolve the owner display name from owner info.
        /// </summary>
        /// <param name="owner"></param>
        /// <returns></returns>
        Task<string?> GetOwnerNameAsync(string owner);
    }
}
