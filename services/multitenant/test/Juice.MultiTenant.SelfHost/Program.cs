using System.IdentityModel.Tokens.Jwt;
using Juice.AspNetCore;
using Juice.MultiTenant;
using Juice.MultiTenant.AspNetCore;
using Juice.MultiTenant.EF;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

//ConfigureSecurity(builder);

ConfigureMultiTenant(builder);

ConfigureDistributedCache(builder.Services, builder.Configuration);

var app = builder.Build();

app.UseRouting();
app.UseMultiTenant();

//app.UseAuthentication();
//app.UseAuthorization();

//app.MapControllerRoute("default", "{__tenant__=}/{controller=Home}/{action=Index}")
//    ;

app.MapGet("/", async (context) =>
{
    var s = context.RequestServices.GetService<ITenant>();
    var roles = context.User.Claims.Where(c => c.Type == "role").Select(c => c.Value);
    var message = $"Hello {s?.Identifier ?? "root"}! {string.Join(',', roles)}";
    await context.Response.WriteAsync(message);
});
//.RequireAuthorization(builder =>
//{
//    builder.RequireAuthenticatedUser();
//});

app.Run();

static void ConfigureMultiTenant(WebApplicationBuilder builder)
{
    var tenantAuthority = builder.Configuration.GetSection("OpenIdConnect:TenantAuthority").Value;
    builder.Services
    .AddMultiTenant()
    .ConfigureTenantEFDirectly(builder.Configuration, options =>
    {
        options.DatabaseProvider = "PostgreSQL";
        options.ConnectionName = "PostgreConnection";
        options.Schema = "App";
    }, builder.Environment.EnvironmentName)
    .WithBasePathStrategy(options => options.RebaseAspNetCorePathBase = true)
    .WithPerTenantOptions<OpenIdConnectOptions>((options, tc) =>
    {
        options.Authority = tenantAuthority?.Replace(Constants.TenantToken, tc.Identifier);
    })
    .WithPerTenantAuthenticationCore()
    //.WithPerTenantAuthenticationConventions(crossTenantAuthorize: (authTenant, currentTenant, principal) =>
    //    authTenant == null // root tenant
    //    && (principal?.Identity?.IsAuthenticated ?? false) // authenticated
    //    && principal.IsInRole("admin"))
    .WithPerTenantAuthenticationConventions(crossTenantAuthorize: null)
    .WithRemoteAuthenticationCallbackStrategy()
    .WithRouteStrategy()
    ;

}


static void ConfigureSecurity(WebApplicationBuilder builder)
{
    var services = builder.Services;
    JwtSecurityTokenHandler.DefaultMapInboundClaims = true;

    var configuration = builder.Configuration;

    services.AddDistributedCacheTicketStore();

    services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        options.DefaultForbidScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie()
    .AddOpenIdConnect(options =>
    {
        options.Authority = configuration.GetSection("OpenIdConnect:Authority").Get<string>();

        options.ClientId = configuration.GetSection("OpenIdConnect:ClientId").Get<string>();
        options.ClientSecret = configuration.GetSection("OpenIdConnect:ClientSecret").Get<string>();

        options.ResponseType = "code";

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("roles");

        options.ClaimActions.MapJsonKey("email_verified", "email_verified");

        options.AuthenticationMethod = OpenIdConnectRedirectBehavior.FormPost;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.SaveTokens = true;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "preferred_username"
        };

        options.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProvider = context =>
            {
                //context.Properties.Items["session_id"] = context.HttpContext.Session.Id;
                return Task.CompletedTask;
            },
            OnRedirectToIdentityProviderForSignOut = OnRedirectToIdentityProviderForSignOut,
            OnAccessDenied = context =>
            {
                context.Response.Redirect("/Account/Auth/AccessDenied");
                return context.Response.CompleteAsync();
            },
            OnRemoteFailure = context =>
            {
                context.Response.Redirect("/Account/Auth/AccessDenied");
                return context.Response.CompleteAsync();
            }
        };

        options.AccessDeniedPath = "/Account/Auth/AccessDenied";
    });

    services.AddAuthorization(options =>
    {
        options.AddPolicy("home", policy =>
        {
            policy.RequireAuthenticatedUser();
        });
    });

}

static async Task OnRedirectToIdentityProviderForSignOut(RedirectContext context)
{
    context.ProtocolMessage.IdTokenHint = await context.HttpContext.GetTokenAsync(OpenIdConnectDefaults.AuthenticationScheme, "id_token");
}

static void ConfigureDistributedCache(IServiceCollection services, IConfiguration configuration)
{
    services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = configuration.GetConnectionString("Redis");
        options.InstanceName = "SampleInstance";
    });
}


