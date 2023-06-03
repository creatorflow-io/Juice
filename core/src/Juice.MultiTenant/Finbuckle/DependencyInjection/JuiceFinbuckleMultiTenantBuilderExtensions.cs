using System.Security.Claims;
using Finbuckle.MultiTenant;
using Juice.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.MultiTenant
{
    public static class JuiceFinbuckleMultiTenantBuilderExtensions
    {

        public static FinbuckleMultiTenantBuilder<TTenantInfo> JuiceIntegration<TTenantInfo>(this FinbuckleMultiTenantBuilder<TTenantInfo> builder)
            where TTenantInfo : class, ITenant, ITenantInfo, new()
        {
            builder.Services.AddScoped<ITenant>(sp => sp.GetService<TTenantInfo>()!);

            return builder;
        }

        public delegate bool CrossTenantAuthorize(string? authTenant, string? currentTenant, ClaimsPrincipal? principal);


        /// <summary>
        /// Override the default behavior of the <see cref="DependencyInjection.FinbuckleMultiTenantBuilderExtensions.WithPerTenantAuthenticationConventions"/> to use dynamic properties of the tenant.
        /// </summary>
        /// <typeparam name="TTenantInfo"></typeparam>
        /// <param name="builder"></param>
        /// <param name="crossTenantAuthorize"></param>
        /// <returns></returns>
        public static FinbuckleMultiTenantBuilder<TTenantInfo> WithPerTenantAuthenticationConventions<TTenantInfo>(
            this FinbuckleMultiTenantBuilder<TTenantInfo> builder,
            CrossTenantAuthorize? crossTenantAuthorize = null
            )
            where TTenantInfo : class, IDynamic, ITenantInfo, new()
        {
            // Set events to set and validate tenant for each cookie based authentication principal.
            builder.Services.ConfigureAll<CookieAuthenticationOptions>(options =>
            {
                // Validate that claimed tenant matches current tenant.
                var origOnValidatePrincipal = options.Events.OnValidatePrincipal;
                options.Events.OnValidatePrincipal = async context =>
                {
                    // Skip if bypass set (e.g. ClaimsStrategy in effect)
                    if (context.HttpContext.Items.Keys.Contains(
                            $"{Constants.TenantToken}__bypass_validate_principal__"))
                        return;

                    var currentTenant = context.HttpContext.GetMultiTenantContext<TTenantInfo>()?.TenantInfo
                        ?.Identifier;
                    string? authTenant = null;
                    if (context.Properties.Items.ContainsKey(Constants.TenantToken))
                    {
                        authTenant = context.Properties.Items[Constants.TenantToken];
                    }
                    else
                    {
                        var loggerFactory = context.HttpContext.RequestServices.GetService<ILoggerFactory>();
                        loggerFactory?.CreateLogger<FinbuckleMultiTenantBuilder<TTenantInfo>>()
                            .LogInformation("No tenant found in authentication properties.");
                    }

                    // Does the current tenant match the auth property tenant?
                    if (!string.Equals(currentTenant, authTenant, StringComparison.OrdinalIgnoreCase)
                        && (crossTenantAuthorize == null
                            || !crossTenantAuthorize(authTenant, currentTenant, context.Principal))
                    )
                    {
                        context.RejectPrincipal();
                    }

                    await origOnValidatePrincipal(context);
                };
            });

            // Set per-tenant cookie options by convention.
            builder.WithPerTenantOptions<CookieAuthenticationOptions>((options, tc) =>
            {
                if (GetPropertyWithValidValue(tc, "CookieLoginPath") is string loginPath)
                    options.LoginPath = loginPath.Replace(Constants.TenantToken, tc.Identifier);

                if (GetPropertyWithValidValue(tc, "CookieLogoutPath") is string logoutPath)
                    options.LogoutPath = logoutPath.Replace(Constants.TenantToken, tc.Identifier);

                if (GetPropertyWithValidValue(tc, "CookieAccessDeniedPath") is string accessDeniedPath)
                    options.AccessDeniedPath = accessDeniedPath.Replace(Constants.TenantToken, tc.Identifier);
            });

            // Set per-tenant OpenIdConnect options by convention.
            builder.WithPerTenantOptions<OpenIdConnectOptions>((options, tc) =>
            {
                if (GetPropertyWithValidValue(tc, "OpenIdConnectAuthority") is string authority)
                    options.Authority = authority.Replace(Constants.TenantToken, tc.Identifier);

                if (GetPropertyWithValidValue(tc, "OpenIdConnectClientId") is string clientId)
                    options.ClientId = clientId.Replace(Constants.TenantToken, tc.Identifier);

                if (GetPropertyWithValidValue(tc, "OpenIdConnectClientSecret") is string clientSecret)
                    options.ClientSecret = clientSecret.Replace(Constants.TenantToken, tc.Identifier);
            });

            builder.WithPerTenantOptions<AuthenticationOptions>((options, tc) =>
            {
                if (GetPropertyWithValidValue(tc, "ChallengeScheme") is string challengeScheme)
                    options.DefaultChallengeScheme = challengeScheme;
            });

            return builder;

            string? GetPropertyWithValidValue(TTenantInfo entity, string propertyName)
            {
                return (entity as IDynamic)?.GetProperty<string?>(() => default, propertyName);
            }
        }

    }
}
