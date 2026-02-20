# Extensions Projects Deep-Dive

## Juice.Extensions.MultiTenant

### TenantInfo
Concrete Finbuckle `ITenantInfo` + Juice `ITenant` + `DynamicModel` (JObject properties):
```csharp
public class TenantInfo : DynamicModel, ITenant, ITenantInfo
{
    string? Id, Name, Identifier, OwnerUser, Tier, Region;
    JObject Properties;    // schema-less extra fields
}
```
`DynamicModel` = `IExpandable`; allows arbitrary tenant metadata stored as JSON.

### FinbuckleTenantAccessor
Thin adapter: wraps `IMultiTenantContextAccessor<TTenant>` → exposes `ITenantAccessor.Tenant`.

### FinbuckleTenantResolver (IScopedTenantResolver)
Used in **non-HTTP** entry points (background services, consumers) to establish tenant context:
```csharp
IDisposable Resolve(string? tenantId)   // looks up tenant from IMultiTenantStore<>
IDisposable Resolve(TTenant? tenant)    // directly sets tenant
```
Pattern:
```csharp
using var scope = _tenantResolver.Resolve(tenantId); // sets MultiTenantContext
// ... work happens within tenant scope ...
// scope.Dispose() restores previous context
```
Internally sets `IMultiTenantContextSetter.MultiTenantContext`; restores previous context on dispose.

### DI Registration
```csharp
services.AddMultiTenant()              // returns MultiTenantBuilder<TenantInfo>
    .AddTenantServices()               // adds IScopedTenantResolver, ITenantAccessor, ITenant (scoped)
    .AddFinbuckleStore(...)            // or other Finbuckle store
    .AddRouteStrategy(...)             // or other Finbuckle strategy
```
`AddTenantServices()` also calls `AddTenantConfiguration()` automatically.
`ITenant` (scoped) = `sp.GetRequiredService<ITenantAccessor>().Tenant` — deprecated, prefer `ITenantAccessor`.

### ConfigureAllPerTenant
```csharp
builder.ConfigureAllPerTenant<TOptions, TTenantInfo>((options, tenant) => { ... });
```
Finbuckle pattern: options configured per active tenant.

---

## Juice.Extensions.Configuration

### ITenantConfiguration : IConfiguration
A tenant-scoped configuration view. Reads from:
1. Root `IConfiguration` (global settings)
2. All registered `IConfigurationSource` singletons (tenant file sources)

### TenantConfiguration (internal)
Lazily rebuilds `IConfiguration` each access by combining root config + registered sources.
Key feature: underscore-to-dot key translation (`"Redis_ConnectionString"` → `"Redis.ConnectionString"`).

### TenantFileConfigurationProvider
Extends `JsonConfigurationProvider`. On `Load(stream)`:
- Reads `ITenantAccessor.Tenant.Identifier`
- Redirects to `tenants/{identifier}/{originalFileName}`
- Silently skips if file not found or no tenant resolved

File layout convention:
```
appsettings.json                      # global settings
tenants/
  mytenant/
    appsettings.json                  # tenant-specific overrides
  othertenant/
    appsettings.json
```

### DI Registration
```csharp
// Auto-called by AddTenantServices():
services.AddTenantConfiguration();   // registers ITenantConfiguration (singleton)

// Add per-tenant JSON override files:
services.AddTenantJsonFile("appsettings.json", optional: true, reloadOnChange: false);
```
`IConfigurationSource` instances are singleton services — `TenantConfiguration` resolves them at read time.

---

## Juice.Extensions.Options

### IOptionsMutable<T> : IOptionsSnapshot<T>
Extends standard options with runtime mutation:
```csharp
public interface IOptionsMutable<out T> : IOptionsSnapshot<T> where T : class, new()
{
    Task<bool> UpdateAsync(Action<T> applyChanges);
}
```

### Two implementations

**OptionsMutable<T>** (global scope):
- Reads from `IOptionsMonitor<T>.CurrentValue`
- Caches updated value for rest of the scoped lifetime
- Saves via `IOptionsMutableStore`

**TenantOptionsMutable<T>** (per-tenant):
- Reads from `ITenantConfiguration.GetSection(key).Get<T>()`
- Saves via `IOptionsMutableStore` (tenant-aware store)

### IOptionsMutableStore
Backend interface for persisting option changes:
```csharp
Task UpdateAsync(string section, object options);
```
`IOptionsMutableStore<T>` — typed marker (preferred over non-generic when registered).

### OptionsMutableJsonFileStore (abstract)
Merges options back to JSON file using `JObject.Merge`:
- Navigates nested JSON by splitting `section` on `:`
- `MergeArrayHandling.Replace, MergeNullValueHandling.Merge`

**TenantOptionsMutableJsonFileStore** — path = `tenants/{tenant.Identifier}/{file}` or `{file}` for root.
```csharp
// Default store file = "appsettings.json"
```

### IOptionsProvider<TService, TOptions>
Typed options holder for specific service types (used to avoid named-options conflicts):
```csharp
public interface IOptionsProvider<TService, TOptions> where TOptions : class
{
    TOptions Value { get; }
}
```
Used by `RedisConnectionProvider` to get `RedisOptions` scoped to itself.

### Registration Extensions
```csharp
// Global mutable options (backed by JSON file store):
services.ConfigureMutable<MyOptions>(config.GetSection("MySection"));

// Per-tenant read-only options (reads from ITenantConfiguration):
services.ConfigurePerTenant<MyOptions>("MySection");

// Per-tenant mutable options (reads + saves to tenant JSON file):
services.ConfigureMutablePerTenant<MyOptions>("MySection");

// For IOptionsProvider pattern:
services.Configure<TService, TOptions>(opts => opts.ConnectionString = "...");
```

---

## Juice.Extensions.Redis

### IRedisConnectionProvider
```csharp
Task<IConnectionMultiplexer> GetConnectionAsync();
```
`IRedisConnectionProvider<T>` — typed variant; allows multiple Redis instances (one per consumer type).

### RedisConnectionProvider (singleton)
- Thread-safe via `SemaphoreSlim`
- Lazy connect on first use; reconnects if disconnected
- Sentinel support: auto-detected by `ServiceName`, `CommandMap.Sentinel`, or port 26379
- Debug mode: enables admin commands + logs endpoint roles

### Registration
```csharp
services.TryAddRedisConnectionProvider(opts => opts.ConnectionString = "...");
services.TryAddRedisConnectionProvider<MyService>(opts => opts.ConnectionString = "...");
```
Uses `IOptionsProvider<TService, RedisOptions>` pattern internally.

---

## Juice.Extensions.Logging

### LoggerProvider (abstract)
Base for custom logging backends. Users subclass and implement `WriteLog<TState>()`:
```csharp
public abstract void WriteLog<TState>(LogEntry<TState> entry, string formattedMessage, IExternalScopeProvider? scopeProvider);
```
Optional hooks: `ScopeStarted`, `ScopeDisposed` — called on `BeginScope` / scope dispose.

### ExternalScopeLoggerProvider
Extends `LoggerProvider` with `ISupportExternalScope` — shares scope provider across all loggers from the same provider instance (enables structured logging correlation).
Override this, NOT `LoggerProvider`, for production use.

### Logger (internal)
- Caches per-category in `LoggerProvider`
- `BeginScope()` wraps in `ScopeWrapper` that calls `Provider.ScopeDisposed` on dispose
- `IsEnabled(logLevel) => true` — filtering deferred to provider

### Pattern for custom log destinations
```csharp
public class FileLoggerProvider : ExternalScopeLoggerProvider
{
    public override void WriteLog<TState>(LogEntry<TState> entry, string formattedMessage, IExternalScopeProvider? scopeProvider)
    {
        // write to file using entry.LogLevel, entry.Category, formattedMessage
    }
}
```

---

## Juice.AspNetCore

### MessageContextMiddleware
HTTP entry point for `MessageContext`:
- Reads `x-correlation-id` header or generates new one
- Calls `MessageContext.Initialize(correlationId, causationId: null, executionId: new, source: appName)`
- Echoes `x-correlation-id` back in response
- `MessageContext.Clear()` in `finally` block (critical — prevents AsyncLocal leak between requests)
```csharp
app.UseMiddleware<MessageContextMiddleware>("my-service-name");
```

### Modular Plugin System (IModuleStartup)
Allows feature modules to self-register without main app changes:
```csharp
public interface IModuleStartup
{
    int StartOrder { get; }      // default 10; controls ConfigureServices order
    int ConfigureOrder { get; }  // default = StartOrder; controls pipeline order
    void ConfigureServices(IServiceCollection services, IMvcBuilder mvc, IWebHostEnvironment env, IConfiguration config);
    ValueTask ConfigurePipelineAsync(IApplicationBuilder app, IEndpointRouteBuilder routes, IWebHostEnvironment env);
    ValueTask ShutdownAsync(IServiceProvider sp, IWebHostEnvironment env);
}
```
`ModuleStartup` = abstract base with virtual no-op implementations.

Discovery via MVC ApplicationParts:
- `StartupDiscoveryFeatureProvider` scans `AssemblyPart` types for `IModuleStartup` impls
- `AddDiscoveredModules(env, config)` — discovers + registers all found modules

```csharp
// Host startup:
services.AddDiscoveredModules(env, configuration)
    .AddApplicationPart(typeof(MyModule).Assembly);
```

### MVC Filters
| Filter | Behavior |
|--------|---------|
| `[RequireTenant]` | Returns 404 if `ITenantAccessor.Tenant == null` |
| `[RootTenant]` | Returns 401 if `ITenantAccessor.Tenant != null` (root-only endpoints) |

### Cookie Auth — DistributedCacheTicketStore
`ITicketStore` implementation that stores serialized auth tickets in `IDistributedCache` instead of the cookie itself:
- Prevents oversized cookies when claims are large
- Key = `AuthSessionStore-{guid}`; sliding expiration = `CookieOptions.ExpireTimeSpan + 10min`
- Logs big tickets (> 500KB unicode) at Debug level

```csharp
services.AddDistributedCacheTicketStore(); // extension in DependencyInjection folder
```

### Pagination Models
```csharp
DatasourceResult<T>  { Page, PageSize, Data (IReadOnlyCollection<T>), Count (long) }
DatasourceRequest    { Page, PageSize, Sort (SortDescriptor[]), ... }
SortDescriptor       { Field, Direction (SortDirection enum) }
```

### Swagger Integration
- `TenantDocsFilter` — hides/shows endpoints by tenant
- `AuthorizeCheckOperationFilter` — shows lock icon on secured endpoints
- `SwaggerIgnoreFilter` — `[SwaggerIgnore]` attribute to hide endpoints
- `ReApplyOptionalRouteParameterOperationFilter` — fixes optional route params in Swagger UI

### GraphQL Support
`GraphQLQuery { Query, OperationName, Variables }` + `AddGraphQL()` service registration extension.

---

## Cross-Cutting Patterns

### Tenant Context Flow
```
HTTP request arrives
  → Finbuckle middleware resolves tenant from route/header/etc
  → Sets IMultiTenantContextAccessor<TenantInfo>.MultiTenantContext
  → FinbuckleTenantAccessor.Tenant = resolved TenantInfo
  → MessageContextMiddleware initializes MessageContext
  → Request handler can inject ITenantAccessor, ITenantConfiguration, IOptionsMutable<T>
```

### Non-HTTP Tenant Context (Background Services / Consumers)
```csharp
// In background service / consumer handler:
using var tenantScope = _tenantResolver.Resolve(tenantId);
// All code here sees the tenant via ITenantAccessor
// Tenant-aware config, options, EF filters all work correctly
```

### Options Layering
```
IConfiguration (global appsettings.json)
  ↓ overridden by
IConfigurationSource (tenants/{id}/appsettings.json)  ← TenantFileConfigurationProvider
  = ITenantConfiguration
      ↓ read by
IOptionsMutable<T> via ConfigurePerTenant / ConfigureMutablePerTenant
      ↓ on UpdateAsync
IOptionsMutableStore → TenantOptionsMutableJsonFileStore → tenants/{id}/appsettings.json
```
