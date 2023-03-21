using Juice.EF.Extensions;
using Juice.Workflows.Api.Domain.CommandHandlers;
using Juice.Workflows.Bpmn.DependencyInjection;
using Juice.Workflows.Domain.AggregatesModel.DefinitionAggregate;
using Juice.Workflows.Domain.Commands;
using Juice.Workflows.EF;
using Juice.Workflows.EF.DependencyInjection;
using Juice.Workflows.Extensions;
using Juice.XUnit;
using Microsoft.EntityFrameworkCore;

namespace Juice.Workflows.Tests
{
    [TestCaseOrderer("Juice.XUnit.PriorityOrderer", "Juice.XUnit")]
    public class EFRepoTests
    {
        private ITestOutputHelper _output;

        public EFRepoTests(ITestOutputHelper output)
        {
            _output = output;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }

        [IgnoreOnCITheory(DisplayName = "DB should migrate"), TestPriority(999)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Should_migrate_Async(string provider)
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            var workflowId = new DefaultStringIdGenerator().GenerateRandomId(6);
            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

                services.AddLocalization(options => options.ResourcesPath = "Resources");

                services.AddDefaultStringIdGenerator();

                services.AddSingleton(provider => _output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddWorkflowDbContext(configuration, options =>
                {
                    options.Schema = "Workflows";
                    options.DatabaseProvider = provider;
                });
                services.AddWorkflowPersistDbContext(configuration, options =>
                {
                    options.Schema = "Workflows";
                    options.DatabaseProvider = provider;
                });
            });

            using var scope = resolver.ServiceProvider.CreateScope();

            var wfContext = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
            await wfContext.MigrateAsync();

            var wfPersistContext = scope.ServiceProvider.GetRequiredService<WorkflowPersistDbContext>();
            await wfPersistContext.MigrateAsync();
        }

        [IgnoreOnCITheory(DisplayName = "Add workflow to DB"), TestPriority(999)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Should_store_workflow_to_db_Async(string provider)
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            var workflowId = new DefaultStringIdGenerator().GenerateRandomId(6);
            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

                services.AddLocalization(options => options.ResourcesPath = "Resources");

                services.AddDefaultStringIdGenerator();

                services.AddSingleton(provider => _output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddWorkflowServices()
                    .AddEFWorkflowRepo()
                    .AddDbWorkflows()
                    ;
                services.AddWorkflowDbContext(configuration, options =>
                {
                    options.Schema = "Workflows";
                    options.DatabaseProvider = provider;
                });

                services.RegisterBpmnWorkflows();

                services.AddMediatR(typeof(StartEvent), typeof(TimerEventStartDomainEventHandler), typeof(StartUserTaskCommandHandler));

            });

            using var scope = resolver.ServiceProvider.CreateScope();

            var contextResolver = scope.ServiceProvider.GetRequiredService<IWorkflowContextResolver>();
            var context = await contextResolver.ResolveAsync("diagram", default);

            context.Should().NotBeNull();
            _output.WriteLine("Builder: " + context.ResolvedBy);
            _output.WriteLine(ContextPrintHelper.Visualize(context));

            var definitionRepo = scope.ServiceProvider.GetRequiredService<IDefinitionRepository>();
            var createResult = await definitionRepo.SaveWorkflowContextAsync(context, "diagram", "BPMN diagram", true, default);
            _output.WriteLine(createResult.ToString());
            createResult.Succeeded.Should().BeTrue();
        }

        [IgnoreOnCITheory(DisplayName = "State should persist to DB"), TestPriority(800)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Should_persist_state_to_db_Async(string provider)
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

                services.AddLocalization(options => options.ResourcesPath = "Resources");

                services.AddDefaultStringIdGenerator();

                services.AddSingleton(provider => _output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddWorkflowServices()
                    .AddEFWorkflowRepo()
                    .AddEFWorkflowStateRepo();

                services.RegisterNodes(typeof(OutcomeBranchUserTask));

                services.AddWorkflowDbContext(configuration, options =>
                {
                    options.Schema = "Workflows";
                    options.DatabaseProvider = provider;
                });
                services.AddWorkflowPersistDbContext(configuration, options =>
                {
                    options.Schema = "Workflows";
                    options.DatabaseProvider = provider;
                });

                services.AddDbWorkflows();

                services.AddMediatR(typeof(StartEvent), typeof(TimerEventStartDomainEventHandler), typeof(StartUserTaskCommandHandler));

                services.RegisterRabbitMQEventBus(configuration.GetSection("RabbitMQ"),
                    options =>
                    {
                        options.BrokerName = "direct.juice_bus";
                        options.SubscriptionClientName = "direct_wf";
                    });

                services.AddSingleton<EventQueue>();

            });
            string? workflowId = default;
            {

                var result = await WorkflowTestHelper.ExecuteAsync(resolver.ServiceProvider, _output, "diagram");

                result.Should().NotBeNull();

                _output.WriteLine(ContextPrintHelper.Visualize(result.Context));

                result.Status.Should().Be(WorkflowStatus.Finished);

                workflowId = result.Context.WorkflowId;
            }
            if (!string.IsNullOrEmpty(workflowId))
            {
                _output.WriteLine("WorkflowId: " + workflowId);

                using var scope = resolver.ServiceProvider.CreateScope();

                var contextResolver = scope.ServiceProvider.GetRequiredService<IWorkflowContextResolver>();

                var context = await contextResolver.StateResolveAsync(workflowId, default, default);

                context.Should().NotBeNull();
                _output.WriteLine("Builder: " + context.ResolvedBy);
                _output.WriteLine(ContextPrintHelper.Visualize(context));
            }
        }

        [IgnoreOnCITheory(DisplayName = "Start workflow from event"), TestPriority(800)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Should_start_from_event_Async(string provider)
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

                services.AddLocalization(options => options.ResourcesPath = "Resources");

                services.AddDefaultStringIdGenerator();

                services.AddSingleton(provider => _output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddWorkflowServices()
                    .AddEFWorkflowRepo()
                    .AddEFWorkflowStateRepo();

                services.RegisterNodes(typeof(OutcomeBranchUserTask));

                services.AddWorkflowDbContext(configuration, options =>
                {
                    options.Schema = "Workflows";
                    options.DatabaseProvider = provider;
                });
                services.AddWorkflowPersistDbContext(configuration, options =>
                {
                    options.Schema = "Workflows";
                    options.DatabaseProvider = provider;
                });

                services.AddDbWorkflows();

                services.AddMediatR(typeof(StartEvent), typeof(TimerEventStartDomainEventHandler), typeof(StartUserTaskCommandHandler));

                services.RegisterRabbitMQEventBus(configuration.GetSection("RabbitMQ"),
                    options =>
                    {
                        options.BrokerName = "direct.juice_bus";
                        options.SubscriptionClientName = "direct_wf";
                    });

                services.AddSingleton<EventQueue>();

            });

            using var scope = resolver.ServiceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
            var eventId = (await dbContext.EventRecords.FirstOrDefaultAsync(e => e.IsStartEvent))?.Id;

            if (eventId.HasValue)
            {
                _output.WriteLine($"Start event {eventId}");
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var rs = await mediator.Send(new DispatchWorkflowEventCommand(eventId.Value, false, default));
                rs.Succeeded.Should().BeTrue();
            }

        }
    }
}
