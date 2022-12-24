using System;
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
    public class ComplexProcessTests
    {
        private ITestOutputHelper _output;

        public ComplexProcessTests(ITestOutputHelper output)
        {
            _output = output;
        }


        [Fact(DisplayName = "Should select all branches")]

        public async Task Should_select_all_paths_Async()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            var branch = "branch3";
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
                        .Parallel("p1")
                            .Fork().SubProcess("P-KB", subBuilder =>
                            {
                                subBuilder.Start().Then<UserTask>("KB").Then<UserTask>("Approve KB").End();
                            }, default).Then<UserTask>("Approve Grph").Seek("p1")
                            .Fork().Then<UserTask>("Editing")
                                .Parallel("p2")
                                    .Fork().Then<ServiceTask>("WEB").Then<UserTask>("Approve Vid")
                                    .Fork().Then<ServiceTask>("Social")
                                    .Merge()
                                        .Fork().Then<ServiceTask>("Copy PS")
                                        .Fork().Then<ServiceTask>("Publish")
                                        .Merge("Copy PS", "Publish", "Approve Grph")
                        .End()
                        ;
                });
            });

            using var scope = resolver.ServiceProvider.CreateScope();
            var workflow = scope.ServiceProvider.GetRequiredService<IWorkflow>();

            var result = await WorkflowTestHelper.ExecuteAsync(workflow, _output, workflowId);

            result.Should().NotBeNull();
            _output.WriteLine(ContextPrintHelper.Visualize(result.Context));

        }
    }
}
