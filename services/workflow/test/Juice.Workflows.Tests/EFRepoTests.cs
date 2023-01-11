using Juice.EF.Extensions;
using Juice.Workflows.Bpmn.DependencyInjection;
using Juice.Workflows.Domain.AggregatesModel.DefinitionAggregate;
using Juice.Workflows.EF;
using Juice.Workflows.EF.DependencyInjection;
using Juice.XUnit;

namespace Juice.Workflows.Tests
{
    [TestCaseOrderer("Juice.XUnit.PriorityOrderer", "Juice.Workflows.Tests")]
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
            });

            using var scope = resolver.ServiceProvider.CreateScope();

            var contextResolver = scope.ServiceProvider.GetRequiredService<IWorkflowContextResolver>();
            var context = await contextResolver.ResolveAsync("diagram", default);

            context.Should().NotBeNull();
            _output.WriteLine("Builder: " + context.ResolvedBy);
            _output.WriteLine(ContextPrintHelper.Visualize(context));

            var definitionRepo = scope.ServiceProvider.GetRequiredService<IDefinitionRepository>();
            if (!await definitionRepo.ExistAsync("diagram", default))
            {
                var definition = new WorkflowDefinition("diagram", "Bpmn diagram");
                definition.SetData(context.Processes,
                    context.Nodes.Values.Select(n => new NodeData(n.Record, n.Node.GetType().Name)),
                    context.Flows.Select(n => new FlowData(n.Record, n.Flow.GetType().Name)));
                var createResult = await definitionRepo.CreateAsync(definition, default);
                createResult.Succeeded.Should().BeTrue();
            }
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

                services.AddMediatR(typeof(StartEvent), typeof(TimerEventStartDomainEventHandler));
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
    }
}
