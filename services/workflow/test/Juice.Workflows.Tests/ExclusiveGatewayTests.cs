﻿namespace Juice.Workflows.Tests
{
    public class ExclusiveGatewayTests
    {
        private ITestOutputHelper _output;

        public ExclusiveGatewayTests(ITestOutputHelper output)
        {
            _output = output;
        }


        [Theory(DisplayName = "Should select single branch")]
        [InlineData("branch1")]
        [InlineData("branch3")]
        [InlineData(default)]
        public async Task Exclusive_gateway_should_select_single_path_Async(string? branch)
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
                       .Exclusive()
                           .Fork().Then<UserTask>("utask_1", "branch1").Then<UserTask>("utask_1_1")
                           .Fork().Then<UserTask>("utask_2").Then<UserTask>("utask_2_1")
                           //.Then<UserTask>("utask_3_2")
                           .Fork().Then<UserTask>("utask_3", "branch3").Then<UserTask>("utask_3_1")
                               .Then<UserTask>("utask_3_2").Then<UserTask>("utask_3_3")
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
                n.Record.Name.StartsWith(
                    branch == "branch1" ? "utask_1"
                    : branch == "branch3" ? "utask_3"
                    : "utask_2")
                || n.Record.Name == "utask_0"
                || n.Node.GetType().IsAssignableTo(typeof(StartEvent))
                || n.Node.GetType().IsAssignableTo(typeof(EndEvent))
                || n.Node.GetType().IsAssignableTo(typeof(ExclusiveGateway)))
                .Select(n => n.Record.Id);

            expectedNodes.Count().Should().BeGreaterThan(4);

            expectedNodes.Should().BeSubsetOf(state?.ExecutedNodes?.Select(n => n.Id));

        }

        [Fact(DisplayName = "Should throw no branch selected")]
        public async Task Exclusive_gateway_should_throw_Async()
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

                services.AddMediatR(options => { options.RegisterServicesFromAssemblyContaining<StartEvent>(); });

                services.AddWorkflowServices()
                    .AddInMemoryReposistories();
                services.RegisterNodes(typeof(OutcomeBranchUserTask));

            });

            using var scope = resolver.ServiceProvider.CreateScope();
            var workflowExecutor = scope.ServiceProvider.GetRequiredService<WorkflowExecutor>();

            var builder = scope.ServiceProvider.GetRequiredService<WorkflowContextBuilder>();
            builder
                .Start()
                .Then<OutcomeBranchUserTask>("utask_0")
                .Exclusive()
                    .Fork().Then<UserTask>("utask_1", "branch1").Then<UserTask>("utask_1_1")
                    .Fork().Then<UserTask>("utask_2", "branch2").Then<UserTask>("utask_2_1")
                    //.Then<UserTask>("utask_3_2")
                    .Fork().Then<UserTask>("utask_3", "branch3").Then<UserTask>("utask_3_1")
                        .Then<UserTask>("utask_3_2").Then<UserTask>("utask_3_3")
                .Merge()
                .End()
                ;
            var context = builder.Build(new Workflows.Domain.AggregatesModel.WorkflowAggregate.WorkflowRecord("axad", "axad", default, default));
            _output.WriteLine(ContextPrintHelper.Visualize(context));

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                var result = await WorkflowTestHelper.ExecuteAsync(workflowExecutor, context, _output, resolver.ServiceProvider);
            });
        }

        [Fact]
        public void Nodes_should_register()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

                services.AddDefaultStringIdGenerator();

                services.AddSingleton(provider => _output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddWorkflowServices();
            });

            using var scope = resolver.ServiceProvider.CreateScope();

            var library = scope.ServiceProvider.GetRequiredService<INodeLibrary>();
            var types = library.GetAllTypes();
            types.Should().NotBeEmpty();
            foreach (var type in types)
            {
                _output.WriteLine(type.Name);
            }
        }

    }

}
