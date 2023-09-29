using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.EF.Extensions;
using Juice.EventBus.IntegrationEventLog.EF;
using Juice.EventBus.RabbitMQ;
using Juice.Extensions.DependencyInjection;
using Juice.Integrations;
using Juice.Integrations.MediatR;
using Juice.MediatR.RequestManager.EF;
using Juice.MultiTenant.Api;
using Juice.MultiTenant.Api.Behaviors.DependencyInjection;
using Juice.MultiTenant.Api.Commands.Tenants;
using Juice.MultiTenant.Domain.AggregatesModel.TenantAggregate;
using Juice.MultiTenant.EF;
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
                services.AddMediatR(options =>
                {
                    options.RegisterServicesFromAssemblyContaining(GetType());
                });
                services.AddTenantDbContext(configuration, options =>
                {
                    options.Schema = "App";
                    options.DatabaseProvider = provider;
                    //options.JsonPropertyBehavior = JsonPropertyBehavior.UpdateALL;
                });

            });

            var context = resolver.ServiceProvider.
                CreateScope().ServiceProvider.GetRequiredService<TenantStoreDbContext>();

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

                services.AddHttpContextAccessor();

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

                services.AddMediatR(options =>
                {
                    options.RegisterServicesFromAssemblyContaining<TenantCommandsTests>();
                    options.RegisterServicesFromAssemblyContaining<CreateTenantCommand>();
                    options.RegisterServicesFromAssemblyContaining<AssemblySelector>();
                });

                services.AddTenantDbContext(configuration, options =>
                {
                    options.Schema = "App";
                    options.DatabaseProvider = provider;
                    //options.JsonPropertyBehavior = JsonPropertyBehavior.UpdateALL;
                });

                services.AddTenantOwnerResolverDefault();

                services.AddOperationExceptionBehavior();
                services.AddMediatRTenantBehaviors();

                services.AddIntegrationEventService()
                        .AddIntegrationEventLog()
                        .RegisterContext<TenantStoreDbContext>("App");

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
                var logContextFactory = scope.ServiceProvider.GetRequiredService<Func<TenantStoreDbContext, IntegrationEventLogContext>>();
                var tenantContext = scope.ServiceProvider.GetRequiredService<TenantStoreDbContext>();
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

            scope.ServiceProvider.RegisterTenantIntegrationEventSelfHandlers<Tenant>();

            var createCommand = new CreateTenantCommand("xunittest", "xunittest", "Test tenant", default, default);

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

            var createCommand = new CreateTenantCommand("xunittest", "xunittest", "Test tenant", default, default);

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


            var updateCommand = new UpdateTenantCommand("xunittest", "test1", "Changed name", default);

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


        [IgnoreOnCITheory(DisplayName = "Aproval tenant"), TestPriority(760)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Tenant_should_accept_Async(string provider)
        {
            using var scope = BuildServiceProvider(_output, provider).
                CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();


            var command = new ApprovalProcessCommand("xunittest", Shared.Enums.TenantStatus.PendingApproval);

            var stopwatch = new Stopwatch();

            stopwatch.Start();

            var result = await mediator.Send(command);

            _output.WriteLine(command.GetType().Name + " take {0} milliseconds.", stopwatch.ElapsedMilliseconds);
            _output.WriteLine(result.Message ?? "");

            result.Succeeded.Should().BeTrue();

            var acceptCommand = new ApprovalProcessCommand("xunittest", Shared.Enums.TenantStatus.Approved);
            stopwatch.Start();

            var acceptResult = await mediator.Send(acceptCommand);

            _output.WriteLine(acceptCommand.GetType().Name + " take {0} milliseconds.", stopwatch.ElapsedMilliseconds);
            _output.WriteLine(acceptResult.Message ?? "");

            acceptResult.Succeeded.Should().BeTrue();
        }


        [IgnoreOnCITheory(DisplayName = "Init tenant"), TestPriority(755)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Tenant_should_init_Async(string provider)
        {
            using var scope = BuildServiceProvider(_output, provider).
                CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();


            var command = new InitializationProcessCommand("xunittest", Shared.Enums.TenantStatus.Initializing);

            var stopwatch = new Stopwatch();

            stopwatch.Start();

            var result = await mediator.Send(command);

            _output.WriteLine(command.GetType().Name + " take {0} milliseconds.", stopwatch.ElapsedMilliseconds);
            _output.WriteLine(result.Message ?? "");

            result.Succeeded.Should().BeTrue();

            var command2 = new InitializationProcessCommand("xunittest", Shared.Enums.TenantStatus.Initialized);
            stopwatch.Start();

            var result2 = await mediator.Send(command2);

            _output.WriteLine(command2.GetType().Name + " take {0} milliseconds.", stopwatch.ElapsedMilliseconds);
            _output.WriteLine(result2.Message ?? "");

            result2.Succeeded.Should().BeTrue();
        }


        [IgnoreOnCITheory(DisplayName = "Active tenant"), TestPriority(750)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Tenant_should_active_Async(string provider)
        {
            using var scope = BuildServiceProvider(_output, provider).
                CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();


            var command = new AdminStatusCommand("xunittest", Shared.Enums.TenantStatus.PendingToActive);

            var stopwatch = new Stopwatch();

            stopwatch.Start();

            var result = await mediator.Send(command);

            _output.WriteLine(command.GetType().Name + " take {0} milliseconds.", stopwatch.ElapsedMilliseconds);
            _output.WriteLine(result.Message ?? "");

            result.Succeeded.Should().BeTrue();

            var command2 = new AdminStatusCommand("xunittest", Shared.Enums.TenantStatus.Active);
            stopwatch.Start();

            var result2 = await mediator.Send(command2);

            _output.WriteLine(command2.GetType().Name + " take {0} milliseconds.", stopwatch.ElapsedMilliseconds);
            _output.WriteLine(result2.Message ?? "");

            result2.Succeeded.Should().BeTrue();
        }


        [IgnoreOnCITheory(DisplayName = "Operation status"), TestPriority(740)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Operation_status_change_Async(string provider)
        {
            using var scope = BuildServiceProvider(_output, provider).
                CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            scope.ServiceProvider.RegisterTenantIntegrationEventSelfHandlers<Tenant>();

            var command = new OperationStatusCommand("xunittest", Shared.Enums.TenantStatus.Inactive);

            var stopwatch = new Stopwatch();

            stopwatch.Start();

            var result = await mediator.Send(command);
            _output.WriteLine(command.GetType().Name + " take {0} milliseconds.", stopwatch.ElapsedMilliseconds);
            _output.WriteLine(result.Message ?? "");
            result.Succeeded.Should().BeTrue();

            await Task.Delay(1000); // waitting for integration events

            var command2 = new OperationStatusCommand("xunittest", Shared.Enums.TenantStatus.Active);
            stopwatch.Start();

            var result2 = await mediator.Send(command2);
            _output.WriteLine(command2.GetType().Name + " take {0} milliseconds.", stopwatch.ElapsedMilliseconds);
            _output.WriteLine(result2.Message ?? "");
            result2.Succeeded.Should().BeTrue();

            await Task.Delay(1000); // waitting for integration events

            var command3 = new OperationStatusCommand("xunittest", Shared.Enums.TenantStatus.Suspended);
            stopwatch.Start();

            var result3 = await mediator.Send(command3);
            _output.WriteLine(command3.GetType().Name + " take {0} milliseconds.", stopwatch.ElapsedMilliseconds);
            _output.WriteLine(result3.Message ?? "");

            result3.Succeeded.Should().BeTrue();
            await Task.Delay(1000);


            result2 = await mediator.Send(command2);
            _output.WriteLine(command2.GetType().Name + " take {0} milliseconds.", stopwatch.ElapsedMilliseconds);
            _output.WriteLine(result2.Message ?? "");
            result2.Succeeded.Should().BeTrue();
        }

        [IgnoreOnCITheory(DisplayName = "Abandon tenant"), TestPriority(730)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Tenant_should_abandoned_Async(string provider)
        {
            using var scope = BuildServiceProvider(_output, provider).
                CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            scope.ServiceProvider.RegisterTenantIntegrationEventSelfHandlers<Tenant>();

            var command = new AbandonTenantCommand("xunittest");

            var stopwatch = new Stopwatch();

            stopwatch.Start();

            var result = await mediator.Send(command);
            _output.WriteLine(command.GetType().Name + " take {0} milliseconds.", stopwatch.ElapsedMilliseconds);
            _output.WriteLine(result.Message ?? "");
            result.Succeeded.Should().BeTrue();

            await Task.Delay(1000); // waitting for integration events
        }

        [IgnoreOnCITheory(DisplayName = "Delete tenant"), TestPriority(700)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Tenant_should_delete_Async(string provider)
        {
            using var scope = BuildServiceProvider(_output, provider).
                CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            scope.ServiceProvider.RegisterTenantIntegrationEventSelfHandlers<Tenant>();

            var command = new DeleteTenantCommand("xunittest");

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
