using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.Extensions.DependencyInjection;
using Juice.Messaging.Idempotency;
using Juice.Messaging.Idempotency.EF;
using Juice.Services;
using Juice.XUnit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Juice.MediatR.Tests
{
    [TestCaseOrderer(typeof(Juice.XUnit.PriorityOrderer))]
    public class IdempotencyRequestTest
    {
        private ITestOutputHelper _testOutput;
        public IdempotencyRequestTest(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }

        #region Setup Helpers

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

                // Register MediatR with idempotency behavior
                services.AddMediatR(cfg =>
                {
                    cfg.RegisterServicesFromAssemblyContaining<IdempotencyRequestTest>();
                    cfg.AddIdempotencyRequestBehavior();
                });

                var builder = services.AddMessaging();
                if (provider == "Redis")
                {
                    builder.AddIdempotencyRedis(options =>
                    {
                        options.ConnectionString = configuration.GetConnectionString("RedisSentinel");
                    });
                }
                else
                {
                    builder.AddIdempotencyEF(configuration, options =>
                    {
                        options.DatabaseProvider = provider;
                        options.Schema = schema;
                        options.ConnectionName = provider == "SqlServer" ? "SqlServerConnection" : "PostgreConnection";
                    });
                }

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

        private async Task CleanupRequestAsync(IServiceProvider serviceProvider, string key)
        {
            try
            {
                var context = serviceProvider.GetService<IdempotencyContext>();
                if (context != null)
                {
                    await context.IdempotencyRecords
                         .Where(r => r.Key == key)
                         .ExecuteDeleteAsync();
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        private async Task SimulateConcurrentRequestsAsync<TRequest>(IServiceProvider serviceProvider, Func<int, TRequest> requestFactory, int n)
            where TRequest : IRequest
        {
            var logger = serviceProvider.GetRequiredService<ILogger<IdempotencyRequestTest>>();

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
                    var request = requestFactory(i);
                    try
                    {
                        await mediator.Send(request);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error handling request: {Message}", ex.Message);
                    }
                }));
            }

            await Task.WhenAll(tasks);
            await Task.Delay(1000); // Allow for async completion
        }

        private async Task<TResponse?[]> SimulateConcurrentRequestsAsync<TResponse>(IServiceProvider serviceProvider, Func<int, IRequest<TResponse>> requestFactory, int n)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<IdempotencyRequestTest>>();

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
                    var request = requestFactory(i);
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
                        logger.LogError(ex, "Error handling request: {Message}", ex.Message);
                    }
                }));
            }

            await Task.WhenAll(tasks);
            await Task.Delay(1000); // Allow for async completion
            return responses.ToArray();
        }

        #endregion

        #region Migration Tests

        [IgnoreOnCITheory(DisplayName = "Schema migration"), TestPriority(10)]
        [InlineData("Contents", "SqlServer")]
        [InlineData("Cms", "PostgreSQL")]
        public async Task Schema_MigrationAsync(string schema, string provider)
        {
            // Arrange
            var resolver = ConfigureServices(schema, provider);

            // Act
            var context = resolver.ServiceProvider.GetRequiredService<IdempotencyContext>();
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();

            if (pendingMigrations.Any())
            {
                _testOutput.WriteLine($"[{schema}][{provider}] You have {pendingMigrations.Count()} pending migrations to apply.");
                _testOutput.WriteLine($"[{schema}][{provider}] Applying pending migrations now");
                await context.Database.MigrateAsync();
            }

            // Assert
            var historyTable = await context.Database.GetAppliedMigrationsAsync();
            historyTable.Count().Should().BeGreaterThan(0);

            _testOutput.WriteLine($"[{schema}][{provider}] Applied {historyTable.Count()} migrations");
        }

        #endregion

        #region Request Manager Tests

        [IgnoreOnCITheory(DisplayName = "Test RequestManager"), TestPriority(1)]
        [InlineData("Cms", "PostgreSQL")]
        [InlineData("Contents", "SqlServer")]
        public async Task RequestManager_TestAsync(string schema, string provider)
        {
            // Arrange
            var resolver = ConfigureServices(schema, provider, services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration(GetType().Assembly);

                // Register DbContext class
                services.AddScoped(sp =>
                {
                    var connectionString = configuration.GetConnectionString("Default");
                    var builder = new DbContextOptionsBuilder<Juice.EF.Tests.Infrastructure.TestContext>();
                    if (provider == "SqlServer")
                    {
                        builder.UseSqlServer(connectionString);
                    }
                    else
                    {
                        builder.UseNpgsql(connectionString);
                    }
                    return new Juice.EF.Tests.Infrastructure.TestContext(sp, builder.Options);
                });
            });

            var requestManager = resolver.ServiceProvider.GetRequiredService<IIdempotencyService>();
            var key = StringIdGenerator.Instance.GenerateUniqueId();

            try
            {
                // Act
                var begin = await requestManager.TryBeginRequestAsync("Request", key);

                // Assert
                begin.Outcome.Should().Be(IdempotencyOutcome.Created);

                await requestManager.TryCompleteRequestAsync("Request", key, true);

                _testOutput.WriteLine($"[{schema}][{provider}] Request {key} completed successfully");
            }
            finally
            {
                await CleanupRequestAsync(resolver.ServiceProvider, key);
            }
        }

        #endregion

        #region Idempotency Behavior Tests

        [IgnoreOnCITheory(DisplayName = "IRequest should handle once"), TestPriority(2)]
        [InlineData("Cms", "PostgreSQL")]
        [InlineData("Contents", "SqlServer")]
        [InlineData("Cms", "Redis")]
        public async Task Request_Should_Be_Handled_OnceAsync(string schema, string provider)
        {
            // Arrange
            var resolver = ConfigureServices(schema, provider);

            var key = StringIdGenerator.Instance.GenerateUniqueId();
            _testOutput.WriteLine($"[{schema}][{provider}] Request key: {key}");

            try
            {
                // Act
                await SimulateConcurrentRequestsAsync(resolver.ServiceProvider, (i) => new Request(key) , 3);

                // Assert
                var sharedService = resolver.ServiceProvider.GetRequiredService<SharedService>();
                sharedService.HandledServices.Count(x => x == nameof(RequestHandler)).Should().Be(1);

                _testOutput.WriteLine($"[{schema}][{provider}] Handler executed exactly once out of 3 concurrent requests");
            }
            finally
            {
                await CleanupRequestAsync(resolver.ServiceProvider, key);
            }
        }

        [IgnoreOnCITheory(DisplayName = "IRequest<T> should handle once"), TestPriority(2)]
        [InlineData("Cms", "PostgreSQL")]
        [InlineData("Contents", "SqlServer")]
        [InlineData("Cms", "Redis")]
        public async Task Request_With_Result_Should_Be_Handled_OnceAsync(string schema, string provider)
        {
            // Arrange
            var resolver = ConfigureServices(schema, provider);
            var key = StringIdGenerator.Instance.GenerateUniqueId();

            _testOutput.WriteLine($"[{schema}][{provider}] Request Id: {key}");

            try
            {
                // Act
                await SimulateConcurrentRequestsAsync(resolver.ServiceProvider, (i) => new RequestWithResult(key), 3);

                // Assert
                var sharedService = resolver.ServiceProvider.GetRequiredService<SharedService>();
                sharedService.HandledServices.Count(x => x == nameof(RequestWithResultHandler)).Should().Be(1);

                _testOutput.WriteLine($"[{schema}][{provider}] Handler executed exactly once out of 3 concurrent requests");
            }
            finally
            {
                await CleanupRequestAsync(resolver.ServiceProvider, key);
            }
        }

        [IgnoreOnCITheory(DisplayName = "IRequest<IOperationResult> should handle once"), TestPriority(2)]
        [InlineData("Cms", "PostgreSQL")]
        [InlineData("Contents", "SqlServer")]
        [InlineData("Cms", "Redis")]
        public async Task Operation_Should_Be_Handled_OnceAsync(string schema, string provider)
        {
            // Arrange
            var resolver = ConfigureServices(schema, provider);
            var key = StringIdGenerator.Instance.GenerateUniqueId();

            _testOutput.WriteLine($"[{schema}][{provider}] Request Id: {key}");

            try
            {
                // Act
                await SimulateConcurrentRequestsAsync(resolver.ServiceProvider, (i) => new Operation(key), 3);

                // Assert
                var sharedService = resolver.ServiceProvider.GetRequiredService<SharedService>();
                sharedService.HandledServices.Count(x => x == nameof(OperationHandler)).Should().Be(1);

                _testOutput.WriteLine($"[{schema}][{provider}] Handler executed exactly once out of 3 concurrent requests");
            }
            finally
            {
                await CleanupRequestAsync(resolver.ServiceProvider, key);
            }
        }

        [IgnoreOnCITheory(DisplayName = "IRequest<IOperationResult<T>> should handle once"), TestPriority(2)]
        [InlineData("Cms", "PostgreSQL")]
        [InlineData("Contents", "SqlServer")]
        [InlineData("Cms", "Redis")]
        public async Task Operation_With_Result_Should_Be_Handled_OnceAsync(string schema, string provider)
        {
            // Arrange
            var resolver = ConfigureServices(schema, provider);
            var key = StringIdGenerator.Instance.GenerateUniqueId();

            _testOutput.WriteLine($"[{schema}][{provider}] Request Id: {key}");

            try
            {
                // Act
                var responses = await SimulateConcurrentRequestsAsync(resolver.ServiceProvider, (i) => new OperationWithResult(key), 3);

                // Assert
                foreach (var response in responses)
                {
                    if (response is null)
                    {
                        _testOutput.WriteLine($"[{schema}][{provider}] No cached result found. The request maybe executing in other thread.");
                        continue;
                    }
                    _testOutput.WriteLine($"[{schema}][{provider}] Response: Succeeded={response.Succeeded}, Data={response.Data}");
                    response.Succeeded.Should().BeTrue();
                    response.Data.Should().Be("Hello World");
                }

                var sharedService = resolver.ServiceProvider.GetRequiredService<SharedService>();
                sharedService.HandledServices.Count(x => x == nameof(OperationWithResultHandler)).Should().Be(1);

                _testOutput.WriteLine($"[{schema}][{provider}] Handler executed exactly once out of 3 concurrent requests");
            }
            finally
            {
                await CleanupRequestAsync(resolver.ServiceProvider, key);
            }
        }

        #endregion

        #region Test Request/Handler Definitions

        private record Request(string IdempotencyKey) : MessageBase, IRequest, IIdempotentRequest;

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

        private record RequestWithResult(string IdempotencyKey) : MessageBase, IRequest<string>, IIdempotentRequest;

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

        private record Operation(string IdempotencyKey) : MessageBase, IRequest<IOperationResult>, IIdempotentRequest;

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

        private record OperationWithResult(string IdempotencyKey) : MessageBase, IRequest<IOperationResult<string>>, IIdempotentRequest;

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

        #endregion
    }
}
