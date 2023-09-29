namespace Juice.Workflows.Tests
{
    public class InclusiveGatewayTests
    {
        private ITestOutputHelper _output;

        public InclusiveGatewayTests(ITestOutputHelper output)
        {
            _output = output;
        }


        [Fact(DisplayName = "Should select branch1 and default")]
        public async Task Inclusive_gateway_should_select_single_path_Async()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            string? branch = "branch1";
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

                services.AddMediatR(options =>
                {
                    options.RegisterServicesFromAssemblyContaining<StartEvent>();
                    options.RegisterServicesFromAssemblyContaining<TimerEventStartDomainEventHandler>();
                });
                services.AddSingleton<EventQueue>();

                services.AddWorkflowServices()
                    .AddInMemoryReposistories();
                services.RegisterNodes(typeof(OutcomeBranchUserTask));

                services.RegisterWorkflow(workflowId, builder =>
                {
                    builder
                        .Start()
                        .Then<OutcomeBranchUserTask>("utask_0")
                        .Inclusive()
                            .Fork().Then<UserTask>("utask_1", "branch1").Then<UserTask>("utask_1_1")
                            .Fork().Then<UserTask>("utask_2", "branch2").Then<UserTask>("utask_2_1")
                            .Fork()
                        .Merge()
                        .End()
                        ;
                });
            });

            var result = await WorkflowTestHelper.ExecuteAsync(resolver.ServiceProvider, _output, workflowId,
                new System.Collections.Generic.Dictionary<string, object?> { { "branch", branch } });

            var context = result.Context;

            context.Should().NotBeNull();

            _output.WriteLine(ContextPrintHelper.Visualize(context));

            result?.Status.Should().Be(WorkflowStatus.Finished);
            var state = context?.State;
            state?.Should().NotBeNull();

            var expectedNodes = context.Nodes.Values
                .Where(n =>
                n.Record.Name.StartsWith("utask_1")
                //|| n.Record.Name.StartsWith("utask_2")
                || n.Record.Name == "utask_0"
                || n.Node.GetType().IsAssignableTo(typeof(StartEvent))
                || n.Node.GetType().IsAssignableTo(typeof(EndEvent))
                || n.Node.GetType().IsAssignableTo(typeof(InclusiveGateway)))
                .Select(n => n.Record.Id);

            expectedNodes.Count().Should().BeGreaterThan(4);

            expectedNodes.Should().BeSubsetOf(state?.ExecutedNodes?.Select(n => n.Id));

        }

    }

}
