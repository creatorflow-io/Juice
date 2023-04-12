using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.EF.Extensions;
using Juice.EF.Tests.EventHandlers;
using Juice.EventBus.IntegrationEventLog.EF;
using Juice.EventBus.IntegrationEventLog.EF.DependencyInjection;
using Juice.EventBus.RabbitMQ.DependencyInjection;
using Juice.Extensions.DependencyInjection;
using Juice.Integrations.EventBus.DependencyInjection;
using Juice.Integrations.MediatR;
using Juice.Integrations.MediatR.DependencyInjection;
using Juice.MediatR.RequestManager.EF;
using Juice.MediatR.RequestManager.EF.DependencyInjection;
using Juice.MultiTenant.Api.Behaviors.DependencyInjection;
using Juice.MultiTenant.Api.Commands;
using Juice.MultiTenant.Api.IntegrationEvents.DependencyInjection;
using Juice.MultiTenant.EF;
using Juice.MultiTenant.EF.DependencyInjection;
using Juice.MultiTenant.EF.Migrations;
using Juice.Services;
using Juice.XUnit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Juice.MultiTenant.Tests
{
    [TestCaseOrderer("Juice.XUnit.PriorityOrderer", "Juice.XUnit")]
    public class TenantCommandsTests
    {
        private readonly ITestOutputHelper _output;

        public TenantCommandsTests(ITestOutputHelper testOutput)
        {
            _output = testOutput;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }

        [IgnoreOnCITheory(DisplayName = "Migrations Request DB"), TestPriority(999)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task TenantRequestDbContext_should_migrate_Async(string provider)
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

                // Register DbContext class

                services.AddRequestManager(configuration, options =>
                {
                    options.ConnectionName = provider switch
                    {
                        "PostgreSQL" => "PostgreConnection",
                        "SqlServer" => "SqlServerConnection",
                        _ => throw new NotSupportedException($"Unsupported provider: {provider}")
                    };
                    options.DatabaseProvider = provider;
                    options.Schema = "App"; // default schema of Tenant
                });

                services.AddDefaultStringIdGenerator();

                services.AddSingleton(provider => _output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });
                services.AddMediatR(typeof(DataEventHandler));
                services.AddTenantDbContext<Tenant>(configuration, options =>
                {
                    options.Schema = "App";
                    options.DatabaseProvider = provider;
                    //options.JsonPropertyBehavior = JsonPropertyBehavior.UpdateALL;
                }, true);

            });

            var context = resolver.ServiceProvider.
                CreateScope().ServiceProvider.GetRequiredService<TenantStoreDbContextWrapper>();

            await context.MigrateAsync();
            await context.SeedAsync(resolver.ServiceProvider.GetRequiredService<IConfigurationService>()
                .GetConfiguration());

            var context1 = resolver.ServiceProvider.
                CreateScope().ServiceProvider.GetRequiredService<ClientRequestContext>();

            var pendingMigrations = await context1.Database.GetPendingMigrationsAsync();

            if (pendingMigrations.Any())
            {
                Console.WriteLine($"[App][ClientRequestContext] You have {pendingMigrations.Count()} pending migrations to apply.");
                Console.WriteLine("[ClientRequestContext] Applying pending migrations now");
                await context1.Database.MigrateAsync();
            }
        }

        static IServiceProvider BuildServiceProvider(ITestOutputHelper output, string provider, bool migrate = false)
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

                // Register DbContext class

                services.AddRequestManager(configuration, options =>
                {
                    options.ConnectionName = provider switch
                    {
                        "PostgreSQL" => "PostgreConnection",
                        "SqlServer" => "SqlServerConnection",
                        _ => throw new NotSupportedException($"Unsupported provider: {provider}")
                    };
                    options.DatabaseProvider = provider;
                    options.Schema = "App"; // default schema of Tenant
                });

                services.AddDefaultStringIdGenerator();

                services.AddSingleton(provider => output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });
                services.AddMediatR(typeof(DataEventHandler));
                services.AddTenantDbContext<Tenant>(configuration, options =>
                {
                    options.Schema = "App";
                    options.DatabaseProvider = provider;
                    //options.JsonPropertyBehavior = JsonPropertyBehavior.UpdateALL;
                }, true);

                services.AddMediatR(typeof(CreateTenantCommand).Assembly, typeof(AssemblySelector).Assembly);

                services.AddOperationExceptionBehavior();
                services.AddMediatRTenantBehaviors();

                services.AddIntegrationEventService()
                        .AddIntegrationEventLog()
                        .RegisterContext<TenantStoreDbContext<Tenant>>("App");

                services.RegisterRabbitMQEventBus(configuration.GetSection("RabbitMQ"));

                services.AddTenantIntegrationEventSelfHandlers<Tenant>();

                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = configuration.GetConnectionString("Redis");
                    options.InstanceName = "SampleInstance";
                });

            });

            if (migrate)
            {
                using var scope = resolver.ServiceProvider.CreateScope();
                var logContextFactory = scope.ServiceProvider.GetRequiredService<Func<TenantStoreDbContext<Tenant>, IntegrationEventLogContext>>();
                var tenantContext = scope.ServiceProvider.GetRequiredService<TenantStoreDbContext<Tenant>>();
                var logContext = logContextFactory(tenantContext);
                logContext.MigrateAsync().GetAwaiter().GetResult();
            }

            return resolver.ServiceProvider;
        }

        [IgnoreOnCITheory(DisplayName = "Migrate EventLogDb"), TestPriority(900)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task EventLogDb_should_create_Async(string provider)
        {
            using var scope = BuildServiceProvider(_output, provider, true).
                CreateScope();
        }

        [IgnoreOnCITheory(DisplayName = "Create tenant"), TestPriority(800)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Tenant_should_create_Async(string provider)
        {
            using var scope = BuildServiceProvider(_output, provider).
                CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            scope.ServiceProvider.RegisterTenantIntegrationEventSelfHandlers();

            var createCommand = new CreateTenantCommand("test", "test", "Test tenant", default);

            var stopwatch = new Stopwatch();

            stopwatch.Start();
            var createResult = await mediator.Send(createCommand);

            _output.WriteLine(createCommand.GetType().Name + " take {0} milliseconds.", stopwatch.ElapsedMilliseconds);
            _output.WriteLine(createResult.Message ?? "");
            createResult.Succeeded.Should().BeTrue();

            stopwatch.Stop();

            await Task.Delay(1000); // waitting for integration events
        }

        [IgnoreOnCITheory(DisplayName = "Create existing tenant"), TestPriority(799)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Tenant_should_not_create_Async(string provider)
        {
            using var scope = BuildServiceProvider(_output, provider).
                CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var createCommand = new CreateTenantCommand("test", "test", "Test tenant", default);

            var stopwatch = new Stopwatch();

            stopwatch.Start();

            var duplicatedCreateResult = await mediator.Send(createCommand);

            _output.WriteLine(createCommand.GetType().Name + " take {0} milliseconds.", stopwatch.ElapsedMilliseconds);

            duplicatedCreateResult.Succeeded.Should().BeFalse();
            duplicatedCreateResult.Message.Should().StartWith($"Failed to handle command {createCommand.GetType().Name}");
            _output.WriteLine(duplicatedCreateResult.Message);

        }

        [IgnoreOnCITheory(DisplayName = "Update tenant"), TestPriority(780)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Tenant_should_update_Async(string provider)
        {
            using var scope = BuildServiceProvider(_output, provider).
                CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();


            var updateCommand = new UpdateTenantCommand("test", "test1", "Changed name", default);

            var stopwatch = new Stopwatch();

            stopwatch.Start();

            var updateResult = await mediator.Send(updateCommand);
            _output.WriteLine(updateCommand.GetType().Name + " take {0} milliseconds.", stopwatch.ElapsedMilliseconds);

            updateResult.Succeeded.Should().BeTrue();

        }

        [IgnoreOnCITheory(DisplayName = "Update not exists"), TestPriority(770)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Tenant_should_not_update_Async(string provider)
        {
            using var scope = BuildServiceProvider(_output, provider).
                CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var updateNotExistsCommand = new UpdateTenantCommand("testnotexist", "test1", "Changed name", default);

            var stopwatch = new Stopwatch();

            stopwatch.Start();

            var updateNotExistsResult = await mediator.Send(updateNotExistsCommand);
            _output.WriteLine(updateNotExistsCommand.GetType().Name + " take {0} milliseconds.", stopwatch.ElapsedMilliseconds);

            updateNotExistsResult.Succeeded.Should().BeFalse();
            updateNotExistsResult.Message.Should()
                .StartWith($"Tenant not found");

        }


        [IgnoreOnCITheory(DisplayName = "Disable tenant"), TestPriority(760)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Tenant_should_disable_Async(string provider)
        {
            using var scope = BuildServiceProvider(_output, provider).
                CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();


            var command = new DisableTenantCommand("test");

            var stopwatch = new Stopwatch();

            stopwatch.Start();

            var disableResult = await mediator.Send(command);
            _output.WriteLine(command.GetType().Name + " take {0} milliseconds.", stopwatch.ElapsedMilliseconds);

            disableResult.Succeeded.Should().BeTrue();
        }

        [IgnoreOnCITheory(DisplayName = "Enable tenant"), TestPriority(750)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Tenant_should_enable_Async(string provider)
        {
            using var scope = BuildServiceProvider(_output, provider).
                CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();


            var command = new EnableTenantCommand("test");

            var stopwatch = new Stopwatch();

            stopwatch.Start();

            var enableResult = await mediator.Send(command);
            _output.WriteLine(command.GetType().Name + " take {0} milliseconds.", stopwatch.ElapsedMilliseconds);
            _output.WriteLine(enableResult.Message ?? "");
            enableResult.Succeeded.Should().BeTrue();
        }


        [IgnoreOnCITheory(DisplayName = "Delete tenant"), TestPriority(700)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Tenant_should_delete_Async(string provider)
        {
            using var scope = BuildServiceProvider(_output, provider).
                CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            scope.ServiceProvider.RegisterTenantIntegrationEventSelfHandlers();

            var command = new DeleteTenantCommand("test");

            var stopwatch = new Stopwatch();

            stopwatch.Start();

            var deleteResult = await mediator.Send(command);
            _output.WriteLine(command.GetType().Name + " take {0} milliseconds.", stopwatch.ElapsedMilliseconds);
            _output.WriteLine(deleteResult.Message ?? "");
            deleteResult.Succeeded.Should().BeTrue();

            await Task.Delay(1000); // waitting for integration events
        }

    }
}
