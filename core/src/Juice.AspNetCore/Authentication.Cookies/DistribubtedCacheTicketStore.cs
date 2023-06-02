using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Juice.AspNetCore.Authentication.Cookies
{
    internal class DistribubtedCacheTicketStore : ITicketStore
    {
        private ISecureDataFormat<AuthenticationTicket> Formatter => _options.TicketDataFormat;
        private const string KeyPrefix = "AuthSessionStore-";
        private CookieAuthenticationOptions _options = new CookieAuthenticationOptions();
        public CookieAuthenticationOptions Options
        {
            get { return _options; }
            set
            {
                _options = value;

                if (_options?.ExpireTimeSpan != null)
                {
                    _slidingExpiration = _options.ExpireTimeSpan.Add(TimeSpan.FromMinutes(10));
                }
            }
        }

        private TimeSpan? _slidingExpiration;
        private IDistributedCache _distributedCache;
        private ILogger _logger;
        public DistribubtedCacheTicketStore(IDistributedCache dbContext,
            ILogger<DistribubtedCacheTicketStore> logger)
        {
            _logger = logger;
            _distributedCache = dbContext;
        }

        public async Task RemoveAsync(string key)
        {
            await _distributedCache.RemoveAsync(key);
        }

        public async Task RenewAsync(string key, AuthenticationTicket ticket)
        {
            var start = DateTimeOffset.Now;
            var options = new DistributedCacheEntryOptions();
            var expiresUtc = ticket.Properties.ExpiresUtc;
            if (expiresUtc.HasValue)
            {
                options.SetAbsoluteExpiration(expiresUtc.Value);
            }
            options.SetSlidingExpiration(_slidingExpiration ?? TimeSpan.FromHours(1)); // TODO: configurable.

            var storeTicket = new AuthenticationTicket(
                new ClaimsPrincipal(ticket.Principal.Identity),
                ticket.Properties,
                ticket.AuthenticationScheme
                );
            var data = Formatter.Protect(storeTicket);

            await _distributedCache.SetStringAsync(key, data, options);

        }

        public async Task<AuthenticationTicket> RetrieveAsync(string key)
        {
            var start = DateTimeOffset.Now;
            var ticketString = await _distributedCache.GetStringAsync(key);

            var rs = ticketString != null ? Formatter.Unprotect(ticketString) : null;

            if (_logger.IsEnabled(LogLevel.Debug) && ticketString != null && ASCIIEncoding.Unicode.GetByteCount(ticketString) > 500000)
            {
                try
                {
                    _logger.LogDebug($"[TicketStore][AuthenticationTicketFactory][BigSize] {JsonConvert.SerializeObject(rs.Properties)}");
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"[TicketStore][AuthenticationTicketFactory][BigSize] Cannot serialize ticket object");
                }
                try
                {
                    var s = JsonConvert.SerializeObject(rs.Principal.Identities);
                    _logger.LogDebug($"[TicketStore][AuthenticationTicketFactory][BigSize2] {s} {ASCIIEncoding.Unicode.GetByteCount(s)}");
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"[TicketStore][AuthenticationTicketFactory][BigSize2] Cannot serialize ticket object");
                }
                try
                {
                    //var s = JsonConvert.SerializeObject(rs.Principal.Claims);
                    _logger.LogDebug($"[TicketStore][AuthenticationTicketFactory][BigSize3] {rs.Principal.Claims.Count()}");
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"[TicketStore][AuthenticationTicketFactory][BigSize3] Cannot serialize ticket object");
                }
            }
            return rs;
        }


        public async Task<string> StoreAsync(AuthenticationTicket ticket)
        {
            var guid = Guid.NewGuid().ToString();
            var key = KeyPrefix + guid;
            await RenewAsync(key, ticket);
            return key;
        }
    }
}
