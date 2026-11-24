using System.Net;
using Juice.MultiTenant;
using System.Text.Json;

namespace Juice.MultiTenant.Testing
{
    /// <summary>
    /// What a service tells <see cref="TenantConformance"/> about itself: one endpoint a member may call in their own tenant, optionally
    /// one only the tenant's admins may call, its role names, and how a <see cref="TestCaller"/> is put on a request.
    /// </summary>
    public sealed class TenantConformanceTarget
    {
        /// <summary>A request a member of the tenant may make (a GET of their own data in it), for a tenant Id.</summary>
        public required Func<Guid, HttpRequestMessage> MemberRequest { get; init; }

        /// <summary>A request only the tenant's admins may make, for a tenant Id; null when the service has none.</summary>
        public Func<Guid, HttpRequestMessage>? AdminRequest { get; init; }

        /// <summary>The service's tenant-admin role (needed with <see cref="AdminRequest"/>).</summary>
        public string? TenantAdminRole { get; init; }

        public string PlatformAdminRole { get; init; } = "platform-admin";

        /// <summary>Roles every member needs to call the member endpoint at all (a service's own viewer role, say).</summary>
        public string[] MemberRoles { get; init; } = [];

        /// <summary>
        /// Puts the caller on the request. The default sends <see cref="TestCaller.Header"/>, for hosts using <c>AddTestCallers()</c>; a service
        /// with its own test tokens mints one from <see cref="TestCaller.Principal"/>.
        /// </summary>
        public Action<HttpRequestMessage, TestCaller> Authorize { get; init; } =
            (request, caller) => request.Headers.Add(TestCaller.HeaderName, caller.Header);

        /// <summary>Whether the service serves several tenants; false for a single-tenant deployment (in <see cref="TestTenants.A"/>).</summary>
        public bool MultiTenant { get; init; } = true;
    }

    /// <summary>
    /// The tenant checks every service passes (the tenant pattern decision record): run it against a test host whose directory holds
    /// <see cref="TestTenants.All"/>, and assert that it returns no failures. Test-framework neutral.
    /// </summary>
    public static class TenantConformance
    {
        public static async Task<IReadOnlyList<string>> CheckAsync(HttpClient client, TenantConformanceTarget target)
        {
            var failures = new List<string>();
            var member = TestCaller.Member(TestTenants.A, target.MemberRoles);

            async Task Expect(string what, Func<Guid, HttpRequestMessage> request, Guid tenant, TestCaller caller, HttpStatusCode? status,
                string? code = null)
            {
                using var message = request(tenant);
                target.Authorize(message, caller);
                using var response = await client.SendAsync(message);
                var body = await response.Content.ReadAsStringAsync();
                if (status is null)
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        failures.Add($"{what}: expected success, got {(int)response.StatusCode} {body}");
                    }
                    return;
                }
                if (response.StatusCode != status)
                {
                    failures.Add($"{what}: expected {(int)status}, got {(int)response.StatusCode} {body}");
                }
                else if (code is not null && Code(body) != code)
                {
                    failures.Add($"{what}: expected code {code}, got {body}");
                }
            }

            await Expect("A member reads their own tenant", target.MemberRequest, TestTenants.A, member, null);
            if (target.MultiTenant)
            {
                await Expect("A member of A names tenant B", target.MemberRequest, TestTenants.B, member, HttpStatusCode.Forbidden, "tenant_forbidden");
                await Expect("A member of A names an unknown tenant", target.MemberRequest, TestTenants.Unknown, member, HttpStatusCode.Forbidden,
                    "tenant_forbidden");
                var platform = TestCaller.Unbound([target.PlatformAdminRole, .. target.MemberRoles]);
                await Expect("A platform admin names an unknown tenant", target.MemberRequest, TestTenants.Unknown, platform, HttpStatusCode.NotFound,
                    "tenant_not_found");
                await Expect("A platform admin names a suspended tenant", target.MemberRequest, TestTenants.B, platform, HttpStatusCode.Forbidden,
                    "tenant_inactive");
                await Expect("A platform admin names a starting tenant", target.MemberRequest, TestTenants.C, platform,
                    HttpStatusCode.ServiceUnavailable, "tenant_starting");
                await Expect("A member of a suspended tenant", target.MemberRequest, TestTenants.B, TestCaller.Member(TestTenants.B, target.MemberRoles),
                    HttpStatusCode.Forbidden, "tenant_inactive");
            }
            else
            {
                await Expect("A member of another tenant", target.MemberRequest, TestTenants.A, TestCaller.Member(TestTenants.B, target.MemberRoles),
                    HttpStatusCode.Forbidden, "tenant_forbidden");
            }

            if (target.AdminRequest is { } admin && target.TenantAdminRole is { } adminRole)
            {
                var tenantAdmin = TestCaller.Member(TestTenants.A, [adminRole, .. target.MemberRoles]);
                await Expect("A tenant admin administers their tenant", admin, TestTenants.A, tenantAdmin, null);
                await Expect("A member who isn't an admin", admin, TestTenants.A, member, HttpStatusCode.Forbidden);
                if (target.MultiTenant)
                {
                    await Expect("A tenant admin of A names tenant B", admin, TestTenants.B, tenantAdmin, HttpStatusCode.Forbidden, "tenant_forbidden");
                    await Expect("A platform admin administers tenant A", admin, TestTenants.A,
                        TestCaller.Unbound([target.PlatformAdminRole, .. target.MemberRoles]), null);
                }
            }
            return failures;
        }

        private static string? Code(string body)
        {
            try
            {
                using var json = JsonDocument.Parse(body);
                return json.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
