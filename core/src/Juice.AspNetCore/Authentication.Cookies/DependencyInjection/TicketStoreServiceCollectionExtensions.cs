using Juice.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Juice.Authentication.Cookies.DependencyInjection
{
    public static class TicketStoreServiceCollectionExtensions
    {
        /// <summary>
        /// Store authentication tickets in a distributed cache.
        /// It maybe contains authentication properties included authenticated tenant.
        /// </summary>
        /// <param name="services"></param>
        public static void AddDistributedCacheTicketStore(this IServiceCollection services)
        {
            services.AddTransient<DistribubtedCacheTicketStore>();
            services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>,
                AspNetCore.Authentication.Cookies.PostConfigureCookieAuthenticationOptions>();
        }
    }
}
