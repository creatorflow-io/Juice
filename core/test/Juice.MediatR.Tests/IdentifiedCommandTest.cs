using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.EF.Tests.Infrastructure;
using Juice.Extensions.DependencyInjection;
using Juice.MediatR.RequestManager.EF;
using Juice.Services;
using Juice.XUnit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Juice.MediatR.Tests
{
    [TestCaseOrderer("Juice.XUnit.PriorityOrderer", "Juice.XUnit")]
    public class IdentifiedCommandTest
    {
        private readonly string ContentSchema = "Contents";
        private readonly string CmsSchema = "Cms";

        private ITestOutputHelper _testOutput;
        public IdentifiedCommandTest(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }

        private DependencyResolver ConfigureServices(string schema, string provider, Action<IServiceCollection>? configure = default)
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration(GetType().Assembly);
                // Register DbContext class
                services.AddMediatR(cfg =>
                {
                    cfg.RegisterServicesFromAssemblyContaining<IdentifiedCommandTest>();
                    cfg.AddEFRequestManager(configuration, options =>
                    {
                        options.DatabaseProvider = provider;
                        options.Schema = schema;
                        options.ConnectionName = provider == "SqlServer" ? "SqlServerConnection" : "PostgreConnection";
                    });
                });
                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger(_testOutput)
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddDefaultStringIdGenerator();

                services.AddSingleton<SharedService>();

                configure?.Invoke(services);
            });
            return resolver;
        }

        [IgnoreOnCIFact(DisplayName = "Contents schema migration"), TestPriority(10)]
        public async Task ContentsSchemaMigrationAsync()
        {
            var schema = ContentSchema;
            var resolver = ConfigureServices(schema, "SqlServer");

            var context = resolver.ServiceProvider.GetRequiredService<ClientRequestContext>();
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();

            if (pendingMigrations.Any())
            {
                Console.WriteLine($"[{schema}][ClientRequestContext] You have {pendingMigrations.Count()} pending migrations to apply.");
                Console.WriteLine("[ClientRequestContext] Applying pending migrations now");
                await context.Database.MigrateAsync();
            }

            var historyTable = await context.Database.GetAppliedMigrationsAsync();
            historyTable.Count().Should().BeGreaterThan(0);
        }

        [IgnoreOnCIFact(DisplayName = "Cms schema migration"), TestPriority(9)]
        public async Task CmsSchemaMigrationAsync()
        {
            var schema = CmsSchema;
            var resolver = ConfigureServices(schema, "PostgreSQL");

            var context = resolver.ServiceProvider.GetRequiredService<ClientRequestContext>();
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();

            if (pendingMigrations.Any())
            {
                Console.WriteLine($"[{schema}][IntegrationEventLogContext] You have {pendingMigrations.Count()} pending migrations to apply.");
                Console.WriteLine("[IntegrationEventLogContext] Applying pending migrations now");
                await context.Database.MigrateAsync();
            }
        }

        [IgnoreOnCIFact(DisplayName = "Test RequestManager"), TestPriority(1)]
        public async Task RequestManagerTestAsync()
        {

            var schema = CmsSchema;

            var resolver = ConfigureServices(schema, "PostgreSQL", services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration(GetType().Assembly);

                // Register DbContext class
                services.AddScoped(provider =>
                {
                    var connectionString = configuration.GetConnectionString("Default");
                    var builder = new DbContextOptionsBuilder<TestContext>();
                    builder.UseSqlServer(connectionString);
                    return new TestContext(provider, builder.Options);
                });
            });

            var logger = resolver.ServiceProvider.GetRequiredService<ILogger<IdentifiedCommandTest>>();

            var context = resolver.ServiceProvider.GetRequiredService<TestContext>();

            var requestManager = resolver.ServiceProvider.GetRequiredService<IRequestManager>(); ;

            var id = Guid.NewGuid();

            try
            {
                var ok = await requestManager.TryCreateRequestForCommandAsync<Request>(id);

                Assert.True(ok);

                await requestManager.TryCompleteRequestAsync<Request>(id, true);
            }
            finally
            {
                // Ensure cleanup in case something failed earlier
                await CleanupRequestAsync(resolver.ServiceProvider, id);
            }
        }

        [IgnoreOnCIFact(DisplayName = "IRequest should handle once"), TestPriority(2)]
        public async Task Request_should_be_handleAsync()
        {

            var schema = CmsSchema;

            var resolver = ConfigureServices(schema, "PostgreSQL");

            var logger = resolver.ServiceProvider.GetRequiredService<ILogger<IdentifiedCommandTest>>();
            var request = new Request(Guid.NewGuid());
            var irequest = new IdentifiedCommand<Request>(request, Guid.NewGuid());

            logger.LogInformation("Request Id: {Id}", request.Id);

            try
            {
                await SimulateConcurrentRequestsAsync(resolver.ServiceProvider, irequest, 3);

                var sharedService = resolver.ServiceProvider.GetRequiredService<SharedService>();
                sharedService.HandledServices.Count(x => x == nameof(RequestHandler)).Should().Be(1);
                sharedService.HandledServices.Count(x => x == nameof(RequestIdentifiedCommandHandler)).Should().Be(3);
            }
            finally
            {
                // cleanup request records created during the test
                await CleanupRequestAsync(resolver.ServiceProvider, irequest.Id);
            }
        }

        [IgnoreOnCIFact(DisplayName = "IRequest<T> should handle once"), TestPriority(2)]
        public async Task Request_with_result_should_be_handleAsync()
        {

            var schema = CmsSchema;
            var resolver = ConfigureServices(schema, "PostgreSQL");

            var logger = resolver.ServiceProvider.GetRequiredService<ILogger<IdentifiedCommandTest>>();
            var request = new RequestWithResult(Guid.NewGuid());
            var irequest = new IdentifiedCommand<RequestWithResult, string>(request, Guid.NewGuid());

            logger.LogInformation("Request Id: {Id}", request.Id);

            try
            {
                await SimulateConcurrentRequestsAsync(resolver.ServiceProvider, irequest, 3);

                var sharedService = resolver.ServiceProvider.GetRequiredService<SharedService>();
                sharedService.HandledServices.Count(x => x == nameof(RequestWithResultHandler)).Should().Be(1);
                sharedService.HandledServices.Count(x => x == nameof(RequestWithResultIdentifiedCommandHandler)).Should().Be(3);
            }
            finally
            {
                await CleanupRequestAsync(resolver.ServiceProvider, irequest.Id);
            }
        }

        [IgnoreOnCIFact(DisplayName = "IRequest<IOperationResult> should handle once"), TestPriority(2)]
        public async Task Operation_should_be_handleAsync()
        {

            var schema = CmsSchema;
            var resolver = ConfigureServices(schema, "PostgreSQL");

            var logger = resolver.ServiceProvider.GetRequiredService<ILogger<IdentifiedCommandTest>>();
            var request = new Operation(Guid.NewGuid());
            var irequest = new IdentifiedCommand<Operation, IOperationResult>(request, Guid.NewGuid());

            logger.LogInformation("Request Id: {Id}", request.Id);
            try
            {
                await SimulateConcurrentRequestsAsync(resolver.ServiceProvider, irequest, 3);
                var sharedService = resolver.ServiceProvider.GetRequiredService<SharedService>();
                sharedService.HandledServices.Count(x => x == nameof(OperationHandler)).Should().Be(1);
                sharedService.HandledServices.Count(x => x == nameof(OperationIdentifiedCommandHandler)).Should().Be(3);
            }
            finally
            {
                await CleanupRequestAsync(resolver.ServiceProvider, irequest.Id);
            }
        }

        [IgnoreOnCIFact(DisplayName = "IRequest<IOperationResult<T>> should handle once"), TestPriority(2)]
        public async Task Operation_with_result_should_be_handleAsync()
        {
            var schema = CmsSchema;

            var resolver = ConfigureServices(schema, "PostgreSQL", services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration(GetType().Assembly);

                services.AddIdentifiedCommandHandler<OperationWithResult, IOperationResult<string>, OperationWithResultHandler, OperationWithResultIdentifiedCommandHandler>();

            });


            var request = new OperationWithResult(Guid.NewGuid());
            var irequest = new IdentifiedCommand<OperationWithResult, IOperationResult<string>>(request, Guid.NewGuid());

            var responses = await SimulateConcurrentRequestsAsync(resolver.ServiceProvider, irequest, 3);
            try
            {
                foreach (var response in responses)
                {
                    if(response is null)
                    {
                        _testOutput.WriteLine("No cached result found. The request maybe executing in other thread.");
                        continue;
                    }
                    _testOutput.WriteLine("Response: Succeeded={0}, Data={1}", response.Succeeded, response.Data);
                    response.Succeeded.Should().BeTrue();
                    response.Data.Should().Be("Hello World");
                }
                var sharedService = resolver.ServiceProvider.GetRequiredService<SharedService>();
                sharedService.HandledServices.Count(x => x == nameof(OperationWithResultHandler)).Should().Be(1);
                sharedService.HandledServices.Count(x => x == nameof(OperationWithResultIdentifiedCommandHandler)).Should().Be(3);
            }
            finally
            {
                await CleanupRequestAsync(resolver.ServiceProvider, irequest.Id);
            }
        }

        private async Task CleanupRequestAsync(IServiceProvider serviceProvider, Guid requestId)
        {
            var context = serviceProvider.GetRequiredService<ClientRequestContext>();
            await context.ClientRequests
                 .Where(r => r.Id == requestId)
                 .ExecuteDeleteAsync();
        }

        private async Task SimulateConcurrentRequestsAsync<TRequest>(IServiceProvider serviceProvider, TRequest request, int n)
            where TRequest : IRequest
        {
            var logger = serviceProvider.GetRequiredService<ILogger<IdentifiedCommandTest>>();

            var barrier = new Barrier(n); // Synchronize n threads
            var tasks = new List<Task>();

            // Act - simulate truly concurrent requests
            for (var i = 0; i < n; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    barrier.SignalAndWait(); // Wait for all threads to be ready
                    using var scope = serviceProvider.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    try
                    {
                        await mediator.Send(request);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex.Message);
                        logger.LogError(ex.StackTrace);
                    }
                }));
            }

            await Task.WhenAll(tasks);
            await Task.Delay(1000); // Allow for async completion
        }

        private async Task<TResponse?[]> SimulateConcurrentRequestsAsync<TResponse>(IServiceProvider serviceProvider, IRequest<TResponse> request, int n)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<IdentifiedCommandTest>>();

            var barrier = new Barrier(n); // Synchronize n threads
            var tasks = new List<Task>();

            // Act - simulate truly concurrent requests
            var responses = new List<TResponse?>();
            for (var i = 0; i < n; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    barrier.SignalAndWait(); // Wait for all threads to be ready
                    using var scope = serviceProvider.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    try
                    {
                        var response = await mediator.Send(request);
                        lock (responses)
                        {
                            responses.Add(response);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex.Message);
                        logger.LogError(ex.StackTrace);
                    }
                }));
            }

            await Task.WhenAll(tasks);
            await Task.Delay(1000); // Allow for async completion
            return responses.ToArray();
        }

        #region request

        private record Request(Guid Id) : IRequest;

        private class RequestHandler : IRequestHandler<Request>
        {
            private readonly SharedService _sharedService;
            public RequestHandler(SharedService sharedService)
            {
                _sharedService = sharedService;
            }
            public ValueTask Handle(Request request, CancellationToken cancellationToken)
            {
                _sharedService.HandledServices.Add(nameof(RequestHandler));
                return ValueTask.CompletedTask;
            }
        }

        private class RequestIdentifiedCommandHandler : IdentifiedCommandHandler<Request>
        {
            public RequestIdentifiedCommandHandler(IMediator mediator, IRequestManager requestManager,
                ILogger<RequestIdentifiedCommandHandler> logger, SharedService sharedService)
                : base(mediator, requestManager, logger)
            {
                sharedService.HandledServices.Add(nameof(RequestIdentifiedCommandHandler));
            }

            protected override (string IdProperty, string CommandId) ExtractDebugInfo(Request command)
                => (nameof(command.Id), command.Id.ToString());
        }

        #endregion

        #region request with result
        private record RequestWithResult(Guid Id) : IRequest<string>;

        private class RequestWithResultHandler : IRequestHandler<RequestWithResult, string>
        {
            private readonly SharedService _sharedService;
            public RequestWithResultHandler(SharedService sharedService)
            {
                _sharedService = sharedService;
            }
            public ValueTask<string> Handle(RequestWithResult request, CancellationToken cancellationToken)
            {
                _sharedService.HandledServices.Add(nameof(RequestWithResultHandler));
                return ValueTask.FromResult("Hello World");
            }
        }

        private class RequestWithResultIdentifiedCommandHandler : IdentifiedCommandHandler<RequestWithResult, string>
        {
            public RequestWithResultIdentifiedCommandHandler(IMediator mediator, IRequestManager requestManager,
                ILogger<RequestWithResultIdentifiedCommandHandler> logger, SharedService sharedService)
                : base(mediator, requestManager, logger)
            {
                sharedService.HandledServices.Add(nameof(RequestWithResultIdentifiedCommandHandler));
            }

            protected override ValueTask<string> CreateResultForDuplicatedRequestAsync(RequestWithResult message)
            {
                return ValueTask.FromResult("Duplicated operation");
            }

            protected override (string IdProperty, string CommandId) ExtractDebugInfo(RequestWithResult command)
                => (nameof(command.Id), command.Id.ToString());
        }

        #endregion

        #region operation

        private record Operation(Guid Id) : IRequest<IOperationResult>
        {
        }

        private class OperationHandler : IRequestHandler<Operation, IOperationResult>
        {
            private readonly SharedService _sharedService;

            public OperationHandler(SharedService sharedService)
            {
                _sharedService = sharedService;
            }

            public ValueTask<IOperationResult> Handle(Operation request, CancellationToken cancellationToken)
            {
                _sharedService.HandledServices.Add(nameof(OperationHandler));
                return ValueTask.FromResult<IOperationResult>(OperationResult.Result("Hello World"));
            }
        }

        private class OperationIdentifiedCommandHandler : IdentifiedCommandHandler<Operation, IOperationResult>
        {
            public OperationIdentifiedCommandHandler(IMediator mediator, IRequestManager requestManager,
                ILogger<OperationIdentifiedCommandHandler> logger, SharedService sharedService)
                : base(mediator, requestManager, logger)
            {
                sharedService.HandledServices.Add(nameof(OperationIdentifiedCommandHandler));
            }

            protected override (string IdProperty, string CommandId) ExtractDebugInfo(Operation command)
                => (nameof(command.Id), command.Id.ToString());
        }
        #endregion

        #region operation with result

        private record OperationWithResult(Guid Id) : IRequest<IOperationResult<string>>
        {
        }

        private class OperationWithResultHandler : IRequestHandler<OperationWithResult, IOperationResult<string>>
        {
            private readonly SharedService _sharedService;

            public OperationWithResultHandler(SharedService sharedService)
            {
                _sharedService = sharedService;
            }

            public ValueTask<IOperationResult<string>> Handle(OperationWithResult request, CancellationToken cancellationToken)
            {
                _sharedService.HandledServices.Add(nameof(OperationWithResultHandler));
                return ValueTask.FromResult(OperationResult.Result("Hello World"));
            }
        }

        private class OperationWithResultIdentifiedCommandHandler : IdentifiedCommandHandler<OperationWithResult, IOperationResult<string>>
        {
            public OperationWithResultIdentifiedCommandHandler(IMediator mediator, IRequestManager requestManager,
                ILogger<OperationWithResultIdentifiedCommandHandler> logger, SharedService sharedService)
                : base(mediator, requestManager, logger)
            {
                sharedService.HandledServices.Add(nameof(OperationWithResultIdentifiedCommandHandler));
            }

            protected override (string IdProperty, string CommandId) ExtractDebugInfo(OperationWithResult command)
                => (nameof(command.Id), command.Id.ToString());
        }
        #endregion

    }
}
