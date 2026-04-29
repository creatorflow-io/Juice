using System;
using System.Threading.Tasks;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FluentAssertions;
using Juice.EF.Tests.Domain;
using Juice.EF.Tests.Infrastructure;
using Juice.Extensions.DependencyInjection;
using TenantInfo = Juice.Extensions.MultiTenant.TenantInfo;
using Juice.MultiTenant;
using Juice.XUnit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Juice.EF.Tests
{
    public class MultitenantDbContextTest
    {
        private readonly ITestOutputHelper _output;

        public MultitenantDbContextTest(ITestOutputHelper testOutput)
        {
            _output = testOutput;
        }

        [IgnoreOnCIFact(DisplayName = "Tenant strict entities should"), TestPriority(1)]
        public async Task Multitenant_dbcontext_shoudAsync()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration(GetType().Assembly);

                services.AddSingleton<SharedService>();
                var connectionString = configuration.GetConnectionString("Default");

                _output.WriteLine("ConnectionString: {0}", connectionString);
                // Register DbContext class
                services.AddDbContext<Juice.EF.Tests.Infrastructure.TestContext>(options =>
                {
                    options.UseSqlServer(connectionString, options =>
                    {
                        options.MigrationsHistoryTable("__EFTestMigrationsHistory", "Contents");
                    });
                });

                services.AddTestTenants<TenantInfo>("tenant-A", "tenant-B");

                services.AddMediatR(options =>
                {
                    options.RegisterServicesFromAssemblyContaining(typeof(EFTest));
                });

                services.AddSingleton(provider => _output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

            });

            await resolver.ServiceProvider.TenantInvokeAsync(async context =>
            {
                var tenantContextAccessor = context.RequestServices.GetRequiredService<IMultiTenantContextAccessor>();
                tenantContextAccessor.MultiTenantContext.Should().NotBeNull();
                tenantContextAccessor.MultiTenantContext.TenantInfo.Should().NotBeNull();
                var tenant = context.RequestServices.GetService<ITenantAccessor>()?.Tenant;
                tenant.Should().NotBeNull();
                var dbContext = context.RequestServices.GetRequiredService<Juice.EF.Tests.Infrastructure.TestContext>();
                dbContext.TenantInfo.Should().NotBeNull();
                _output.WriteLine("dbContext.TenantInfo: {0}", dbContext.TenantInfo!.Identifier);
            }, "tenant-A");

            var id = Guid.NewGuid();

            // Init data in tenant-A
            await resolver.ServiceProvider.TenantInvokeAsync(async context =>
            {
                var dbContext = context.RequestServices.GetRequiredService<Juice.EF.Tests.Infrastructure.TestContext>();
                // Add entity
                dbContext.Add(new CrossTenantContent(id, "Init name"));
                await dbContext.SaveChangesAsync();
            }, "tenant-A");

            // Verify data in tenant-A
            await resolver.ServiceProvider.TenantInvokeAsync(async context =>
            {
                var dbContext = context.RequestServices.GetRequiredService<Juice.EF.Tests.Infrastructure.TestContext>();
                var entity = await dbContext.Set<CrossTenantContent>()
                    .FirstOrDefaultAsync(e => e.Id == id);
                entity.Should().NotBeNull();
                entity!.Name.Should().Be("Init name");
            }, "tenant-A");

            // Verify data NOT VISIBLE in tenant-B
            await resolver.ServiceProvider.TenantInvokeAsync(async context =>
            {
                var dbContext = context.RequestServices.GetRequiredService<Juice.EF.Tests.Infrastructure.TestContext>();
                var entity = await dbContext.Set<CrossTenantContent>()
                    .FirstOrDefaultAsync(e => e.Id == id);
                entity.Should().BeNull();
            }, "tenant-B");

            // Verify data NOT VISIBLE in root tenant
            await resolver.ServiceProvider.TenantInvokeAsync(async context =>
            {
                var dbContext = context.RequestServices.GetRequiredService<Juice.EF.Tests.Infrastructure.TestContext>();
                var entity = await dbContext.Set<CrossTenantContent>()
                    .FirstOrDefaultAsync(e => e.Id == id);
                entity.Should().BeNull();
            }, null);

            // Update data in tenant-A
            await resolver.ServiceProvider.TenantInvokeAsync(async context =>
            {
                var dbContext = context.RequestServices.GetRequiredService<Juice.EF.Tests.Infrastructure.TestContext>();
                var entity = await dbContext.Set<CrossTenantContent>()
                    .FirstOrDefaultAsync(e => e.Id == id);
                entity.Should().NotBeNull();
                entity!.UpdateName("Modified name in tenant-A");
                await dbContext.SaveChangesAsync();
            }, "tenant-A");

            // Clean up data in tenant-A
            await resolver.ServiceProvider.TenantInvokeAsync(async context =>
            {
                var dbContext = context.RequestServices.GetRequiredService<Juice.EF.Tests.Infrastructure.TestContext>();
                var entity = await dbContext.Set<CrossTenantContent>()
                    .FirstOrDefaultAsync(e => e.Id == id);
                if (entity != null)
                {
                    dbContext.Remove(entity);
                    await dbContext.SaveChangesAsync();
                }
            }, "tenant-A");

            // Init data in root tenant
            await resolver.ServiceProvider.TenantInvokeAsync(async context =>
            {
                var dbContext = context.RequestServices.GetRequiredService<Juice.EF.Tests.Infrastructure.TestContext>();
                // Add entity
                dbContext.Add(new CrossTenantContent(id, "Init name in root tenant"));
                await dbContext.SaveChangesAsync();
            }, null);

            // Verify data NOT VISIBLE in tenant-A
            await resolver.ServiceProvider.TenantInvokeAsync(async context =>
            {
                var dbContext = context.RequestServices.GetRequiredService<Juice.EF.Tests.Infrastructure.TestContext>();
                var entity = await dbContext.Set<CrossTenantContent>()
                    .FirstOrDefaultAsync(e => e.Id == id);
                entity.Should().BeNull();
            }, "tenant-A");

            // Update data in root tenant
            await resolver.ServiceProvider.TenantInvokeAsync(async context =>
            {
                var dbContext = context.RequestServices.GetRequiredService<Juice.EF.Tests.Infrastructure.TestContext>();
                var entity = await dbContext.Set<CrossTenantContent>()
                    .FirstOrDefaultAsync(e => e.Id == id);
                entity.Should().NotBeNull();
                entity!.UpdateName("Modified name in root tenant");
                await dbContext.SaveChangesAsync();
            }, null);

            // Clean up data in root tenant
            await resolver.ServiceProvider.TenantInvokeAsync(async context =>
            {
                var dbContext = context.RequestServices.GetRequiredService<Juice.EF.Tests.Infrastructure.TestContext>();
                var entity = await dbContext.Set<CrossTenantContent>()
                    .FirstOrDefaultAsync(e => e.Id == id);
                if (entity != null)
                {
                    dbContext.Remove(entity);
                    await dbContext.SaveChangesAsync();
                }
            }, null);
        }

        [IgnoreOnCIFact(DisplayName = "Tenant shared entities should"), TestPriority(2)]
        public async Task TenantShared_dbcontext_shouldAsync()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration(GetType().Assembly);

                services.AddSingleton<SharedService>();
                var connectionString = configuration.GetConnectionString("Default");

                _output.WriteLine("ConnectionString: {0}", connectionString);
                // Register DbContext class
                services.AddDbContext<TenantSharedTestContext>(options =>
                {
                    options.UseSqlServer(connectionString, options =>
                    {
                    });
                });

                services.AddTestTenants<TenantInfo>("tenant-A", "tenant-B");

                services.AddMediatR(options =>
                {
                    options.RegisterServicesFromAssemblyContaining(typeof(EFTest));
                });

                services.AddSingleton(provider => _output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

            });

            var id = Guid.NewGuid();

            // Init shared data in root tenant
            await resolver.ServiceProvider.TenantInvokeAsync(async context =>
            {
                var dbContext = context.RequestServices.GetRequiredService<TenantSharedTestContext>();
                // Add entity
                dbContext.Add(new CrossTenantContent(id, "Init shared name"));
                await dbContext.SaveChangesAsync();
            }, null);

            // Verify shared data VISIBLE in tenant-A but cannot be modified or deleted
            await resolver.ServiceProvider.TenantInvokeAsync(async context =>
            {
                var dbContext = context.RequestServices.GetRequiredService<TenantSharedTestContext>();
                var entity = await dbContext.Set<CrossTenantContent>()
                    .FirstOrDefaultAsync(e => e.Id == id);
                entity.Should().NotBeNull();
                entity!.Name.Should().Be("Init shared name");

                entity!.UpdateName("Modified name in tenant-A");
                await FluentActions.Awaiting(async () => await dbContext.SaveChangesAsync())
                    .Should().ThrowAsync<MultiTenantException>();

                await FluentActions.Awaiting(async () =>
                {
                    dbContext.Remove(entity);
                    await dbContext.SaveChangesAsync();
                }).Should().ThrowAsync<MultiTenantException>();
            }, "tenant-A");

            // Verify shared data VISIBLE in tenant-B but cannot be modified or deleted
            await resolver.ServiceProvider.TenantInvokeAsync(async context =>
            {
                var dbContext = context.RequestServices.GetRequiredService<TenantSharedTestContext>();
                var entity = await dbContext.Set<CrossTenantContent>()
                    .FirstOrDefaultAsync(e => e.Id == id);
                entity.Should().NotBeNull();
                entity!.Name.Should().Be("Init shared name");

                entity!.UpdateName("Modified name in tenant-B");
                await FluentActions.Awaiting(async () => await dbContext.SaveChangesAsync())
                    .Should().ThrowAsync<MultiTenantException>();

                await FluentActions.Awaiting(async () =>
                {
                    dbContext.Remove(entity);
                    await dbContext.SaveChangesAsync();
                }).Should().ThrowAsync<MultiTenantException>();
            }, "tenant-B");

            // Verify shared data VISIBLE in root tenant and can be modified
            await resolver.ServiceProvider.TenantInvokeAsync(async context =>
            {
                var dbContext = context.RequestServices.GetRequiredService<TenantSharedTestContext>();
                var entity = await dbContext.Set<CrossTenantContent>()
                    .FirstOrDefaultAsync(e => e.Id == id);
                entity.Should().NotBeNull();
                entity!.Name.Should().Be("Init shared name");
                entity!.UpdateName("Modified name in root tenant");
                await dbContext.SaveChangesAsync();
            }, null);

            // Verify shared data modified in root tenant is visible in tenant-B
            await resolver.ServiceProvider.TenantInvokeAsync(async context =>
            {
                var dbContext = context.RequestServices.GetRequiredService<TenantSharedTestContext>();
                var entity = await dbContext.Set<CrossTenantContent>()
                    .FirstOrDefaultAsync(e => e.Id == id);
                entity.Should().NotBeNull();
                entity!.Name.Should().Be("Modified name in root tenant");
            }, "tenant-B");

            // Clean up data in root tenant
            await resolver.ServiceProvider.TenantInvokeAsync(async context =>
            {
                var dbContext = context.RequestServices.GetRequiredService<TenantSharedTestContext>();
                var entity = await dbContext.Set<CrossTenantContent>()
                    .FirstOrDefaultAsync(e => e.Id == id);
                if (entity != null)
                {
                    dbContext.Remove(entity);
                    await dbContext.SaveChangesAsync();
                }
            }, null);
        }

        [IgnoreOnCIFact(DisplayName = "Global shared entities should"), TestPriority(3)]
        public async Task GlobalShared_dbcontext_shouldAsync()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration(GetType().Assembly);

                services.AddSingleton<SharedService>();
                var connectionString = configuration.GetConnectionString("Default");

                _output.WriteLine("ConnectionString: {0}", connectionString);
                // Register DbContext class
                services.AddDbContext<GlobalSharedTestContext>(options =>
                {
                    options.UseSqlServer(connectionString, options =>
                    {
                    });
                });

                services.AddTestTenants<TenantInfo>("tenant-A", "tenant-B");

                services.AddMediatR(options =>
                {
                    options.RegisterServicesFromAssemblyContaining(typeof(EFTest));
                });

                services.AddSingleton(provider => _output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

            });

            var id = Guid.NewGuid();

            // Init global shared data in tenant-A
            await resolver.ServiceProvider.TenantInvokeAsync(async context =>
            {
                var dbContext = context.RequestServices.GetRequiredService<GlobalSharedTestContext>();
                // Add entity
                dbContext.Add(new CrossTenantContent(id, "Init global shared name"));
                await dbContext.SaveChangesAsync();
            }, "tenant-A");

            // Verify global shared data NOT VISIBLE in tenant-B
            await resolver.ServiceProvider.TenantInvokeAsync(async context =>
            {
                var dbContext = context.RequestServices.GetRequiredService<GlobalSharedTestContext>();
                var entity = await dbContext.Set<CrossTenantContent>()
                    .FirstOrDefaultAsync(e => e.Id == id);
                entity.Should().BeNull();
            }, "tenant-B");

            // Verify global shared data VISIBLE in root tenant and can be modified
            await resolver.ServiceProvider.TenantInvokeAsync(async context =>
            {
                var dbContext = context.RequestServices.GetRequiredService<GlobalSharedTestContext>();
                var entity = await dbContext.Set<CrossTenantContent>()
                    .FirstOrDefaultAsync(e => e.Id == id);
                entity.Should().NotBeNull();
                entity!.Name.Should().Be("Init global shared name");

                entity!.UpdateName("Modified name in root tenant");
                await dbContext.SaveChangesAsync();
            }, null);

            // Verify global shared data modified in root tenant is VISIBLE in tenant-A
            await resolver.ServiceProvider.TenantInvokeAsync(async context =>
            {
                var dbContext = context.RequestServices.GetRequiredService<GlobalSharedTestContext>();
                var entity = await dbContext.Set<CrossTenantContent>()
                    .FirstOrDefaultAsync(e => e.Id == id);
                entity.Should().NotBeNull();
                entity!.Name.Should().Be("Modified name in root tenant");
            }, "tenant-A");

            // Verify global shared data modified in root tenant is NOT VISIBLE in tenant-B
            await resolver.ServiceProvider.TenantInvokeAsync(async context =>
            {
                var dbContext = context.RequestServices.GetRequiredService<GlobalSharedTestContext>();
                var entity = await dbContext.Set<CrossTenantContent>()
                    .FirstOrDefaultAsync(e => e.Id == id);
                entity.Should().BeNull();
            }, "tenant-B");

            // Clean up global shared data in root tenant
            await resolver.ServiceProvider.TenantInvokeAsync(async context =>
            {
                var dbContext = context.RequestServices.GetRequiredService<GlobalSharedTestContext>();
                var entity = await dbContext.Set<CrossTenantContent>()
                    .FirstOrDefaultAsync(e => e.Id == id);

                dbContext.Remove(entity!);
                await dbContext.SaveChangesAsync();
            }, null);

            // Verify global shared data is removed in tenant-A
            await resolver.ServiceProvider.TenantInvokeAsync(async context =>
            {
                var dbContext = context.RequestServices.GetRequiredService<GlobalSharedTestContext>();
                var entity = await dbContext.Set<CrossTenantContent>()
                    .FirstOrDefaultAsync(e => e.Id == id);
                entity.Should().BeNull();
            }, "tenant-A");
        }
    }

}
