﻿using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.Extensions.DependencyInjection;
using Juice.Services;
using Juice.Workflows.DependencyInjection;
using Juice.Workflows.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Juice.Workflows.Tests
{
    public class ParallelGatewayTests
    {
        private ITestOutputHelper _output;

        public ParallelGatewayTests(ITestOutputHelper output)
        {
            _output = output;
        }


        [Fact(DisplayName = "Should run all branch")]
        public async Task Parallel_gateway_should_select_all_path_Async()
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
                    .AddInMemoryReposistories();
                //services.RegisterNodes(typeof(OutcomeBranchUserTask));
                services.RegisterWorkflow(workflowId, builder =>
                {
                    builder
                        .Start()
                        .Then<UserTask>("utask_0")
                        .Parallel()
                            .Fork().Then<UserTask>("utask_1").Then<UserTask>("utask_1_1")
                            //.Fork().Then<UserTask>("utask_2").Then<UserTask>("utask_2_1")
                            //.Then<UserTask>("utask_3_2")
                            .Fork().Then<UserTask>("utask_3").Then<UserTask>("utask_3_1")
                                .Then<UserTask>("utask_3_2").Then<UserTask>("utask_3_3")
                        .Merge()
                        .End()
                        ;
                });
            });

            using var scope = resolver.ServiceProvider.CreateScope();
            var workflow = scope.ServiceProvider.GetRequiredService<IWorkflow>();

            var result = await WorkflowTestHelper.ExecuteAsync(workflow, _output, workflowId);

            _output.WriteLine(ContextPrintHelper.Visualize(workflow.ExecutedContext));

            workflow.ExecutedContext.Should().NotBeNull();

            result?.Status.Should().Be(WorkflowStatus.Finished);
            var state = workflow.ExecutedContext?.State;
            state?.Should().NotBeNull();

            var expectedNodes = workflow.ExecutedContext.Nodes.Values
                .Where(n =>
                n.Record.Name.StartsWith("utask_1")
                || n.Record.Name.StartsWith("utask_3")
                || n.Record.Name == "utask_0"
                || n.Node.GetType().IsAssignableTo(typeof(StartEvent))
                || n.Node.GetType().IsAssignableTo(typeof(EndEvent))
                || n.Node.GetType().IsAssignableTo(typeof(ParallelGateway)))
                .Select(n => n.Record.Id);

            expectedNodes.Count().Should().BeGreaterThan(4);

            expectedNodes.Should().BeSubsetOf(state?.ExecutedNodes?.Select(n => n.Id));
        }

        [Fact(DisplayName = "Multiple in-out flows")]
        public async Task Parallel_gateway_multiple_in_out_Async()
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
                    .AddInMemoryReposistories();
                services.RegisterNodes(typeof(OutcomeBranchUserTask));

                services.RegisterWorkflow(workflowId, builder =>
                {
                    builder
                        .Start()
                        .Then<UserTask>("utask_0")
                        .Parallel()
                            .Fork().Then<UserTask>("utask_1")
                            .Fork().Then<UserTask>("utask_3")
                        .Merge()
                            .Fork().Then<UserTask>("utask_1_1")
                            .Fork().Then<UserTask>("utask_3_1")
                        .Merge()
                        .End()
                        ;
                });
            });

            using var scope = resolver.ServiceProvider.CreateScope();
            var workflow = scope.ServiceProvider.GetRequiredService<IWorkflow>();

            var result = await WorkflowTestHelper.ExecuteAsync(workflow, _output, workflowId);

            _output.WriteLine(ContextPrintHelper.Visualize(workflow.ExecutedContext));

            workflow.ExecutedContext.Should().NotBeNull();

            result?.Status.Should().Be(WorkflowStatus.Finished);
            var state = workflow.ExecutedContext?.State;
            state?.Should().NotBeNull();

            var expectedNodes = workflow.ExecutedContext.Nodes.Values
                .Where(n =>
                n.Record.Name.StartsWith("utask_1")
                || n.Record.Name.StartsWith("utask_3")
                || n.Record.Name == "utask_0"
                || n.Node.GetType().IsAssignableTo(typeof(StartEvent))
                || n.Node.GetType().IsAssignableTo(typeof(EndEvent))
                || n.Node.GetType().IsAssignableTo(typeof(ParallelGateway)))
                .Select(n => n.Record.Id);

            expectedNodes.Count().Should().BeGreaterThan(4);

            expectedNodes.Should().BeSubsetOf(state?.ExecutedNodes?.Select(n => n.Id));
        }

    }

}
