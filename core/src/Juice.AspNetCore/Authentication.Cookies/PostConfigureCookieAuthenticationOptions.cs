using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace Juice.AspNetCore.Authentication.Cookies
{
    internal class PostConfigureCookieAuthenticationOptions : IPostConfigureOptions<CookieAuthenticationOptions>
    {
        private readonly DistribubtedCacheTicketStore _ticketStore;
        public PostConfigureCookieAuthenticationOptions(DistribubtedCacheTicketStore ticketStore)
        {
            _ticketStore = ticketStore;
        }

        public void PostConfigure(string name, CookieAuthenticationOptions options)
        {
            _ticketStore.Options = options;
            options.SessionStore = (ITicketStore?)_ticketStore;
        }
    }
}
