using Juice.AspNetCore.Mvc.Formatters;
using Juice.EventBus.RabbitMQ;
using Juice.Extensions.Swagger;
using Juice.MultiTenant;
using Juice.MultiTenant.Api;
using Juice.MultiTenant.Api.Grpc.Services;
using Juice.MultiTenant.Domain.AggregatesModel.TenantAggregate;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

var builder = WebApplication.CreateBuilder(args);

ConfigureMultiTenant(builder);

ConfigureGRPC(builder.Services);

ConfigureEvents(builder);

ConfigureDistributedCache(builder.Services, builder.Configuration);

ConfigureSecurity(builder);

ConfigureSwagger(builder);

ConfigureOrigins(builder);

// For unit test
builder.Services.AddScoped<TenantStoreService>();

builder.Services.AddControllers(options =>
{
    options.InputFormatters.Add(new TextSingleValueFormatter());
}).AddNewtonsoftJson(options =>
{
    options.SerializerSettings.Converters.Add(new StringEnumConverter());
});

var app = builder.Build();

app.UseCors("AllowAllOrigins");
app.UseMultiTenant();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapTenantGrpcServices();
app.RegisterTenantIntegrationEventSelfHandlers();

UseTenantSwagger(app);

app.MapControllers();

app.MapGet("/", () => "Support gRPC only!");

// For unit test
app.MapGet("/tenant", async (context) =>
{
    var s = context.RequestServices.GetService<ITenant>();
    if (s == null)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await context.Response.WriteAsync(JsonConvert.SerializeObject(s, new ExpandoObjectConverter()));
});

app.Run();

static void ConfigureMultiTenant(WebApplicationBuilder builder)
{
    builder.Services
    .AddMultiTenant()
    .ConfigureTenantHost(builder.Configuration, options =>
    {
        options.DatabaseProvider = "PostgreSQL";
        options.ConnectionName = "PostgreConnection";
        options.Schema = "App";
    }).WithBasePathStrategy(options => options.RebaseAspNetCorePathBase = true)
    .WithHeaderStrategy()
    .WithRouteStrategy()
    ;

    builder.Services.AddTenantIntegrationEventSelfHandlers<Tenant>();

    builder.Services.AddTenantOwnerResolverDefault();
}

static void ConfigureGRPC(IServiceCollection services)
{
    // Add services to the container.
    services.AddGrpc(o => o.EnableDetailedErrors = true);
}

static void ConfigureEvents(WebApplicationBuilder builder)
{

    builder.Services.RegisterRabbitMQEventBus(builder.Configuration.GetSection("RabbitMQ"),
       options =>
       {
           options.BrokerName = "topic.juice_bus";
           options.SubscriptionClientName = "juice_multitenant_test_host_events";
           options.ExchangeType = "topic";
       });

}

static void ConfigureDistributedCache(IServiceCollection services, IConfiguration configuration)
{
    services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = configuration.GetConnectionString("Redis");
        options.InstanceName = "SampleInstance";
    });
}

static void ConfigureSecurity(WebApplicationBuilder builder)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
        {
            options.Authority = builder.Configuration.GetSection("OpenIdConnect:Authority").Get<string>();
            options.Audience = builder.Configuration.GetSection("OpenIdConnect:Audience").Get<string?>();
            options.RequireHttpsMetadata = false;
        });

    builder.Services.AddTenantAuthorizationTest();
}

static void ConfigureOrigins(WebApplicationBuilder builder)
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAllOrigins",
                       builder =>
                       {
                           builder.AllowAnyOrigin()
                           .AllowAnyMethod()
                           .AllowAnyHeader();
                       });
    });
}

static void ConfigureSwagger(WebApplicationBuilder builder)
{

    builder.Services.AddApiVersioning(setup =>
    {
        setup.DefaultApiVersion = new ApiVersion(2, 0);
        setup.AssumeDefaultVersionWhenUnspecified = true;
        setup.ReportApiVersions = true;
    });

    builder.Services.AddVersionedApiExplorer(setup =>
    {
        setup.GroupNameFormat = "'v'VVV";
        setup.SubstituteApiVersionInUrl = true;
    });

    builder.Services.ConfigureSwaggerApiOptions(builder.Configuration.GetSection("Api"));
    builder.Services.AddSwaggerGen(c =>
    {
        c.IgnoreObsoleteActions();

        c.IgnoreObsoleteProperties();

        c.SchemaFilter<SwaggerIgnoreFilter>();

        c.UseInlineDefinitionsForEnums();

        c.DescribeAllParametersInCamelCase();

        c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Flows = new OpenApiOAuthFlows
            {
                AuthorizationCode = new OpenApiOAuthFlow
                {
                    AuthorizationUrl = new Uri(builder.Configuration.GetSection("OpenIdConnect:Authority").Get<string>() + "/connect/authorize"),
                    TokenUrl = new Uri(builder.Configuration.GetSection("OpenIdConnect:Authority").Get<string>() + "/connect/token"),
                    Scopes = new Dictionary<string, string>
                    {
                        { "openid", "OpenId" },
                        { "profile", "Profile" },
                        { "tenants-api", "Tenants API" }
                    }
                }
            },
            Scheme = "Bearer"
        });

        c.OperationFilter<AuthorizeCheckOperationFilter>();
        c.OperationFilter<ReApplyOptionalRouteParameterOperationFilter>();
        c.DocumentFilter<TenantDocsFilter>();
    });

    builder.Services.AddSwaggerGenNewtonsoftSupport();

    builder.Services.ConfigureSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Version = "v1",
            Title = "Tenants API V1",
            Description = "Provide Tenants Management Web API"
        });

        c.SwaggerDoc("v2", new OpenApiInfo
        {
            Version = "v2",
            Title = "Tenants API V2",
            Description = "Provide Tenants Management Web API"
        });

        c.IncludeReferencedXmlComments();
    });

}

static void UseTenantSwagger(WebApplication app)
{
    app.UseSwagger(options => options.RouteTemplate = "tenants/swagger/{documentName}/swagger.json");
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("v1/swagger.json", "Tenants API V1");
        c.SwaggerEndpoint("v2/swagger.json", "Tenants API V2");
        c.RoutePrefix = "tenants/swagger";

        c.OAuthClientId("tenants_api_swaggerui");
        c.OAuthAppName("Tenants API Swagger UI");
        c.OAuthUsePkce();
    });
}




