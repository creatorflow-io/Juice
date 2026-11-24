using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Juice.MultiTenant.Testing
{
    /// <summary>
    /// A caller in a test: a subject, the tenant its token names (or none) and its roles. Send it with <see cref="Header"/> to a host using
    /// <c>AddTestCallers()</c>, or turn it into your own token with <see cref="Principal"/>.
    /// </summary>
    public sealed record TestCaller(string Subject, Guid? Tenant, params string[] Roles)
    {
        public const string HeaderName = "X-Test-Caller";

        /// <summary>A person of the tenant.</summary>
        public static TestCaller Member(Guid tenant, params string[] roles) => new("person-" + tenant.ToString("N")[..8], tenant, roles);

        /// <summary>A caller whose token names no tenant, with the roles (a platform admin, a service).</summary>
        public static TestCaller Unbound(params string[] roles) => new("service", null, roles);

        /// <summary>The header value: <c>sub=…;tenant=…;roles=a,b</c>.</summary>
        public string Header => $"sub={Subject};tenant={Tenant?.ToString("D")};roles={string.Join(',', Roles)}";

        public ClaimsPrincipal Principal(string tenantClaim = "platform_tenant", string roleClaim = "roles")
        {
            var claims = new List<Claim> { new("sub", Subject) };
            if (Tenant is { } tenant)
            {
                claims.Add(new Claim(tenantClaim, tenant.ToString("D")));
            }
            claims.AddRange(Roles.Select(r => new Claim(roleClaim, r)));
            return new ClaimsPrincipal(new ClaimsIdentity(claims, TestCallerAuthentication.Scheme, "sub", roleClaim));
        }

        internal static TestCaller? Parse(string value)
        {
            var parts = value.Split(';').Select(p => p.Split('=', 2)).Where(p => p.Length == 2).ToDictionary(p => p[0].Trim(), p => p[1].Trim());
            if (!parts.TryGetValue("sub", out var sub) || sub.Length == 0)
            {
                return null;
            }
            Guid? tenant = parts.TryGetValue("tenant", out var t) && Guid.TryParse(t, out var id) ? id : null;
            var roles = parts.TryGetValue("roles", out var r) ? r.Split(',', StringSplitOptions.RemoveEmptyEntries) : [];
            return new TestCaller(sub, tenant, roles);
        }
    }

    /// <summary>An authentication scheme reading <see cref="TestCaller.HeaderName"/>; for test hosts only.</summary>
    public sealed class TestCallerAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Scheme = "TestCaller";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(TestCaller.HeaderName, out var value))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }
            var caller = TestCaller.Parse(value.ToString());
            return Task.FromResult(caller is null
                ? AuthenticateResult.Fail("Unreadable test caller")
                : AuthenticateResult.Success(new AuthenticationTicket(caller.Principal(), Scheme)));
        }
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    using Juice.MultiTenant.Testing;

    public static class TestCallerServiceCollectionExtensions
    {
        /// <summary>Authenticates <see cref="TestCaller"/>s by their header, as the default scheme. Test hosts only.</summary>
        public static AuthenticationBuilder AddTestCallers(this IServiceCollection services)
            => services.AddAuthentication(TestCallerAuthentication.Scheme)
                .AddScheme<AuthenticationSchemeOptions, TestCallerAuthentication>(TestCallerAuthentication.Scheme, null);
    }
}
