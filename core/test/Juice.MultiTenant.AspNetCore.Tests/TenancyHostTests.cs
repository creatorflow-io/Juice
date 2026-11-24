using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Juice.MultiTenant.Testing;
using Finbuckle.MultiTenant;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TenantInfo = Juice.Extensions.MultiTenant.TenantInfo;

namespace Juice.MultiTenant.AspNetCore.Tests
{
    public class TenancyHostTests
    {
        private const string AdminRole = "things-tenant-admin";

        /// <summary>A small service: a member endpoint and an admin endpoint per tenant, one without a tenant, and a controller.</summary>
        private static async Task<WebApplication> Host(Action<TenancyBuilder>? tenancy = null, Action<TenancyOptions>? options = null)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddTestCallers();
            builder.Services.AddAuthorization();
            builder.Services.AddControllers().AddApplicationPart(typeof(TenancyHostTests).Assembly);
            var t = builder.Services.AddJuiceTenancy(configure: o =>
            {
                o.TenantAdminRole = AdminRole;
                options?.Invoke(o);
            });
            if (tenancy is null)
            {
                t.WithTestTenants();
            }
            else
            {
                tenancy(t);
            }

            var app = builder.Build();
            app.UseRouting();
            app.UseAuthentication();
            app.UseJuiceTenancy();
            app.UseAuthorization();
            var tenants = app.MapGroup("/tenants/{tenantId}").RequireAuthorization();
            tenants.MapGet("/me", (ITenantContext t) => new { tenant = t.Tenant!.IdText, source = t.Source.ToString() }).RequireTenantMember();
            tenants.MapGet("/settings", (ITenantContext t) => new { tenant = t.Tenant!.IdText }).RequireTenantAdmin();
            app.MapGet("/current", (ITenantContext t) => new { tenant = t.Tenant?.IdText, source = t.Source?.ToString() }).RequireAuthorization();
            app.MapGet("/needs-tenant", () => "ok").RequireAuthorization().RequireTenant();
            // A service's own endpoint: it decides who may act in the tenant (as a gRPC service would, by policy).
            app.MapGet("/tenants/{tenantId}/produce", (ITenantContext t) => new { tenant = t.Tenant!.IdText })
                .RequireAuthorization(TenancyPolicies.Tenant);
            app.MapGet("/tenants/{tenantId}/policy-member", () => "ok").RequireAuthorization(TenancyPolicies.Member);
            app.MapGet("/public/{tenantId}", (ITenantContext t) => new { tenant = t.Tenant?.IdText });
            app.MapGet("/finbuckle/{tenantId}", (HttpContext h) =>
                new { finbuckle = h.RequestServices.GetService<Finbuckle.MultiTenant.Abstractions.IMultiTenantContextAccessor>()?.MultiTenantContext?.TenantInfo?.Id })
                .RequireTenantMember();
            app.MapControllers();
            await app.StartAsync();
            return app;
        }

        private static HttpRequestMessage Get(string path, TestCaller? caller = null, Action<HttpRequestMessage>? more = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (caller is not null)
            {
                request.Headers.Add(TestCaller.HeaderName, caller.Header);
            }
            more?.Invoke(request);
            return request;
        }

        private static async Task<(HttpStatusCode Status, string Body)> Send(WebApplication app, HttpRequestMessage request)
        {
            using var response = await app.GetTestClient().SendAsync(request);
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task A_multi_tenant_service_passes_the_shared_tenant_checks()
        {
            await using var app = await Host();
            var failures = await TenantConformance.CheckAsync(app.GetTestClient(), new TenantConformanceTarget
            {
                MemberRequest = id => Get($"/tenants/{id}/me"),
                AdminRequest = id => Get($"/tenants/{id}/settings"),
                TenantAdminRole = AdminRole,
            });
            failures.Should().BeEmpty();
        }

        [Fact]
        public async Task A_member_is_in_their_claims_tenant_and_a_platform_admin_in_the_one_they_name()
        {
            await using var app = await Host();

            var mine = await Send(app, Get("/current", TestCaller.Member(TestTenants.A)));
            mine.Body.Should().Contain(TestTenants.A.ToString("D")).And.Contain("Claim");

            var named = await Send(app, Get("/current", TestCaller.Unbound("platform-admin"), r => r.Headers.Add("X-Tenant-Id", TestTenants.A.ToString())));
            named.Body.Should().Contain(TestTenants.A.ToString("D")).And.Contain("Header");

            // A platform admin naming nothing is in no tenant, and an endpoint needing one says so.
            var none = await Send(app, Get("/current", TestCaller.Unbound("platform-admin")));
            none.Body.Should().Contain("\"tenant\":null");
            var required = await Send(app, Get("/needs-tenant", TestCaller.Unbound("platform-admin")));
            required.Status.Should().Be(HttpStatusCode.BadRequest);
            required.Body.Should().Contain("tenant_required");
        }

        [Fact]
        public async Task A_member_naming_another_tenant_in_the_header_is_refused()
        {
            await using var app = await Host();
            var other = await Send(app, Get("/current", TestCaller.Member(TestTenants.A), r => r.Headers.Add("X-Tenant-Id", TestTenants.B.ToString())));
            other.Status.Should().Be(HttpStatusCode.Forbidden);
            other.Body.Should().Contain("tenant_forbidden");
        }

        [Fact]
        public void A_claim_that_isnt_a_tenant_id_names_no_other_tenant()
        {
            // A token whose claim holds an Identifier (an older token): the test scheme writes only GUIDs, so build the caller by hand.
            var oldToken = new TestCaller("old", null);
            var principal = oldToken.Principal();
            principal.Identities.First().AddClaim(new System.Security.Claims.Claim("platform_tenant", "acme"));
            var context = new DefaultHttpContext { User = principal };
            context.Request.Headers["X-Tenant-Id"] = TestTenants.A.ToString();
            var (rejection, tenant, _) = TenantResolutionMiddleware.Locate(context, new TenancyOptions());
            rejection!.Code.Should().Be("tenant_forbidden");
            tenant.Should().BeNull();
        }

        [Fact]
        public async Task An_anonymous_caller_may_not_name_a_tenant_and_learns_nothing_about_it()
        {
            await using var app = await Host();
            // An endpoint needing a signed-in caller answers 401 (sign in again), not a tenant answer.
            (await Send(app, Get($"/tenants/{TestTenants.B}/me"))).Status.Should().Be(HttpStatusCode.Unauthorized);
            foreach (var tenant in new[] { TestTenants.A, TestTenants.B, TestTenants.Unknown })
            {
                var response = await Send(app, Get($"/public/{tenant}"));
                response.Status.Should().Be(HttpStatusCode.Forbidden);
                response.Body.Should().Contain("tenant_forbidden");
            }

            await using var open = await Host(options: o => o.AnonymousMayNameTenant = true);
            (await Send(open, Get($"/public/{TestTenants.A}"))).Body.Should().Contain(TestTenants.A.ToString("D"));
            foreach (var tenant in new[] { TestTenants.B, TestTenants.C, TestTenants.Unknown })
            {
                var response = await Send(open, Get($"/public/{tenant}"));
                response.Status.Should().Be(HttpStatusCode.Forbidden, "suspended, starting and unknown all look the same");
                response.Body.Should().Contain("tenant_forbidden");
            }
        }

        [Fact]
        public async Task A_service_whose_token_names_no_tenant_names_one_and_its_endpoint_decides()
        {
            await using var app = await Host();
            var service = TestCaller.Unbound("producer");
            var produce = await Send(app, Get($"/tenants/{TestTenants.A}/produce", service));
            produce.Status.Should().Be(HttpStatusCode.OK);
            produce.Body.Should().Contain(TestTenants.A.ToString("D"));
            // But it isn't a member, by policy as by filter.
            (await Send(app, Get($"/tenants/{TestTenants.A}/policy-member", service))).Status.Should().Be(HttpStatusCode.Forbidden);
            (await Send(app, Get($"/tenants/{TestTenants.A}/policy-member", TestCaller.Member(TestTenants.A)))).Status.Should().Be(HttpStatusCode.OK);
            (await Send(app, Get($"/tenants/{TestTenants.B}/policy-member", TestCaller.Member(TestTenants.A)))).Status.Should().Be(HttpStatusCode.Forbidden);

            await using var strict = await Host(options: o => o.RequireClaim = true);
            (await Send(strict, Get($"/tenants/{TestTenants.A}/produce", service))).Status.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task The_endpoint_checks_the_claim_again_for_a_caller_signed_in_after_the_middleware()
        {
            // The middleware saw no caller (the scheme runs at the endpoint); the endpoint's check still refuses another tenant.
            var http = new DefaultHttpContext();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddJuiceTenancy().WithTestTenants();
            http.RequestServices = services.BuildServiceProvider();
            var context = http.RequestServices.GetRequiredService<TenantContext>();
            context.Tenant = TestTenants.ActiveA;
            http.User = TestCaller.Member(TestTenants.B).Principal();
            (await TenantRequirements.CheckAsync(http, TenantRequirement.Tenant))!.Code.Should().Be("tenant_forbidden");
            http.User = TestCaller.Member(TestTenants.A).Principal();
            (await TenantRequirements.CheckAsync(http, TenantRequirement.Tenant)).Should().BeNull();
        }

        [Fact]
        public async Task An_endpoint_with_its_own_scheme_has_its_caller_decided_before_the_tenant()
        {
            // No default scheme: the caller is signed in only by the endpoint's scheme, as a gRPC service's JWT scheme would.
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddAuthentication()
                .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestCallerAuthentication>(TestCallerAuthentication.Scheme, null);
            builder.Services.AddAuthorization();
            builder.Services.AddJuiceTenancy().WithTestTenants();
            await using var app = builder.Build();
            app.UseRouting();
            app.UseAuthentication();
            app.UseJuiceTenancy();
            app.UseAuthorization();
            app.MapGet("/tenants/{tenantId}/x", (ITenantContext t) => new { tenant = t.Tenant!.IdText })
                .RequireAuthorization(new Microsoft.AspNetCore.Authorization.AuthorizeAttribute { AuthenticationSchemes = TestCallerAuthentication.Scheme })
                .RequireTenant();
            await app.StartAsync();

            var platform = await Send(app, Get($"/tenants/{TestTenants.A}/x", TestCaller.Unbound("platform-admin")));
            platform.Status.Should().Be(HttpStatusCode.OK);
            platform.Body.Should().Contain(TestTenants.A.ToString("D"));
            var other = await Send(app, Get($"/tenants/{TestTenants.B}/x", TestCaller.Member(TestTenants.A)));
            other.Status.Should().Be(HttpStatusCode.Forbidden);
            other.Body.Should().Contain("tenant_forbidden");
            var unknown = await Send(app, Get($"/tenants/{TestTenants.Unknown}/x", TestCaller.Unbound("platform-admin")));
            unknown.Status.Should().Be(HttpStatusCode.NotFound, "the platform admin was recognised before the lookup");
        }

        [Fact]
        public async Task Without_a_directory_the_host_refuses_to_start()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddJuiceTenancy();
            var app = builder.Build();
            var start = () => app.UseJuiceTenancy();
            start.Should().Throw<InvalidOperationException>().WithMessage("*directory*");
            await app.DisposeAsync();
        }

        [Fact]
        public async Task A_tenant_that_isnt_an_id_is_a_bad_request()
        {
            await using var app = await Host();
            var route = await Send(app, Get("/tenants/acme/me", TestCaller.Member(TestTenants.A)));
            route.Status.Should().Be(HttpStatusCode.BadRequest);
            route.Body.Should().Contain("tenant_invalid");
        }

        [Fact]
        public async Task The_old_header_counts_only_when_it_holds_an_id()
        {
            await using var app = await Host();
            var platform = TestCaller.Unbound("platform-admin");
            var byId = await Send(app, Get("/current", platform, r => r.Headers.Add("__tenant__", TestTenants.A.ToString())));
            byId.Body.Should().Contain(TestTenants.A.ToString("D"));
            var byIdentifier = await Send(app, Get("/current", platform, r => r.Headers.Add("__tenant__", "tenant-a")));
            byIdentifier.Body.Should().Contain("\"tenant\":null");
        }

        [Fact]
        public async Task A_route_tenant_in_base32_form_is_the_same_tenant()
        {
            await using var app = await Host();
            var base32 = new Juice.Services.StringIdGenerator().GenerateUniqueId(TestTenants.A);
            var response = await Send(app, Get($"/tenants/{base32}/me", TestCaller.Member(TestTenants.A)));
            response.Status.Should().Be(HttpStatusCode.OK);
            response.Body.Should().Contain(TestTenants.A.ToString("D"));
        }

        [Fact]
        public async Task A_single_tenant_deployment_passes_the_shared_tenant_checks()
        {
            await using var app = await Host(t => t.WithTestTenants().WithFixedTenant(TestTenants.A), o => o.RequireClaim = true);
            var failures = await TenantConformance.CheckAsync(app.GetTestClient(), new TenantConformanceTarget
            {
                MemberRequest = _ => Get("/current"),
                MultiTenant = false,
            });
            failures.Should().BeEmpty();

            var noClaim = await Send(app, Get("/current", TestCaller.Unbound()));
            noClaim.Status.Should().Be(HttpStatusCode.Forbidden);
            var mine = await Send(app, Get("/current", TestCaller.Member(TestTenants.A)));
            mine.Body.Should().Contain(TestTenants.A.ToString("D"));
            var naming = await Send(app, Get("/current", TestCaller.Unbound("platform-admin"), r => r.Headers.Add("X-Tenant-Id", TestTenants.B.ToString())));
            naming.Status.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task A_controller_can_require_the_tenant()
        {
            await using var app = await Host();
            (await Send(app, Get($"/things/{TestTenants.A}", TestCaller.Member(TestTenants.A)))).Status.Should().Be(HttpStatusCode.OK);
            var admin = await Send(app, Get($"/things/{TestTenants.A}/admin", TestCaller.Member(TestTenants.A)));
            admin.Status.Should().Be(HttpStatusCode.Forbidden);
            admin.Body.Should().Contain("tenant_forbidden");
            (await Send(app, Get($"/things/{TestTenants.A}/admin", TestCaller.Member(TestTenants.A, AdminRole)))).Status.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task A_finbuckle_store_is_the_directory_and_its_status_counts()
        {
            await using var app = await Host(t =>
            {
                t.Services.AddMultiTenant<TenantInfo>().WithInMemoryStore(o =>
                {
                    o.Tenants.Add(new TenantInfo { Id = TestTenants.A.ToString("D"), Identifier = "tenant-a", Name = "A", Status = "Active" });
                    o.Tenants.Add(new TenantInfo { Id = TestTenants.B.ToString("D"), Identifier = "tenant-b", Name = "B", Status = "Suspended" });
                });
                t.WithStore<TenantInfo>();
            });
            (await Send(app, Get($"/tenants/{TestTenants.A}/me", TestCaller.Member(TestTenants.A)))).Status.Should().Be(HttpStatusCode.OK);
            var suspended = await Send(app, Get($"/tenants/{TestTenants.B}/me", TestCaller.Member(TestTenants.B)));
            suspended.Status.Should().Be(HttpStatusCode.Forbidden);
            suspended.Body.Should().Contain("tenant_inactive");

            var directory = app.Services.GetRequiredService<ITenantDirectory>();
            (await directory.FindByIdentifierAsync("tenant-a"))!.Id.Should().Be(TestTenants.A);
            (await directory.AvailabilityAsync(TestTenants.Unknown)).Should().Be(TenantAvailability.Inactive);
        }

        [Fact]
        public async Task A_store_still_keyed_by_the_old_form_finds_the_tenant_and_finbuckle_gets_it_without_a_second_lookup()
        {
            var old = TenantIds.FormatBase32(TestTenants.A);
            await using var app = await Host(t =>
            {
                t.Services.AddMultiTenant<TenantInfo>().AddTenantServices().WithInMemoryStore(o =>
                    o.Tenants.Add(new TenantInfo { Id = old, Identifier = "tenant-a", Name = "A", Status = "Active" }));
                t.WithStore<TenantInfo>();
            });
            var response = await Send(app, Get($"/finbuckle/{TestTenants.A}", TestCaller.Member(TestTenants.A)));
            response.Status.Should().Be(HttpStatusCode.OK);
            response.Body.Should().Contain(old, "Finbuckle's context holds the store's tenant");
        }

        [Fact]
        public async Task The_directory_remembers_a_tenant_until_it_is_invalidated()
        {
            var tenant = new TenantInfo { Id = TestTenants.A.ToString("D"), Identifier = "tenant-a", Name = "A", Status = "Active" };
            await using var app = await Host(t =>
            {
                t.Services.AddMultiTenant<TenantInfo>().WithInMemoryStore(o => o.Tenants.Add(tenant));
                t.WithStore<TenantInfo>();
            });
            var directory = app.Services.GetRequiredService<ITenantDirectory>();
            (await directory.FindAsync(TestTenants.A))!.Availability.Should().Be(TenantAvailability.Active);

            var store = app.Services.CreateScope().ServiceProvider.GetRequiredService<Finbuckle.MultiTenant.Abstractions.IMultiTenantStore<TenantInfo>>();
            var stored = await store.TryGetAsync(TestTenants.A.ToString("D"));
            stored!.Status = "Suspended";
            await store.TryUpdateAsync(stored);

            (await directory.FindAsync(TestTenants.A))!.Availability.Should().Be(TenantAvailability.Active, "it is remembered");
            app.Services.GetRequiredService<ITenantDirectoryCache>().Invalidate(TestTenants.A);
            (await directory.FindAsync(TestTenants.A))!.Availability.Should().Be(TenantAvailability.Inactive);
        }
    }

    [ApiController]
    [Route("things/{tenantId}")]
    [RequireTenant(TenantRequirement.Member)]
    public class ThingsController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get([FromServices] ITenantContext tenant) => Ok(new { tenant = tenant.Tenant!.IdText });

        [HttpGet("admin")]
        [RequireTenant(TenantRequirement.Admin)]
        public IActionResult Admin() => Ok();
    }
}
