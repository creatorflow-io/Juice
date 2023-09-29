namespace Juice.Workflows.Tests
{
    public class WorkflowContextAccessorTest
    {
        private ITestOutputHelper _output;

        public WorkflowContextAccessorTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact(DisplayName = "Context should access")]
        public async Task Context_should_access_Async()
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

                services.AddMediatR(options => { options.RegisterServicesFromAssemblyContaining<StartEvent>(); });

            });

            using var scope = resolver.ServiceProvider.CreateScope();
            var accessor = scope.ServiceProvider.GetRequiredService<IWorkflowContextAccessor>();
            accessor.SetWorkflowId(workflowId);

        }
    }
}
