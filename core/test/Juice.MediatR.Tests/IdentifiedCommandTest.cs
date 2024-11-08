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
using MediatR;
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

        [IgnoreOnCIFact(DisplayName = "Contents schema migration"), TestPriority(10)]
        public async Task ContentsSchemaMigrationAsync()
        {

            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            var schema = ContentSchema;

            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration(GetType().Assembly);

                // Register DbContext class

                services.AddEFMediatorRequestManager(configuration, options =>
                {
                    options.DatabaseProvider = "SqlServer";
                    options.Schema = schema;
                    options.ConnectionName = "SqlServerConnection";
                });

                services.AddSingleton(provider => _testOutput);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

            });

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
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            var schema = CmsSchema;

            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration(GetType().Assembly);

                // Register DbContext class

                services.AddEFMediatorRequestManager(configuration, options =>
                {
                    options.DatabaseProvider = "PostgreSQL";
                    options.Schema = schema;
                    options.ConnectionName = "PostgreConnection";
                });

                services.AddSingleton(provider => _testOutput);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

            });

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
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            var schema = CmsSchema;

            resolver.ConfigureServices(services =>
            {

                services.AddSingleton(provider => _testOutput);

                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration(GetType().Assembly);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                // Register DbContext class
                services.AddScoped(provider =>
                {
                    var connectionString = configuration.GetConnectionString("Default");
                    var builder = new DbContextOptionsBuilder<TestContext>();
                    builder.UseSqlServer(connectionString);
                    return new TestContext(provider, builder.Options);
                });

                services.AddDefaultStringIdGenerator();

                services.AddEFMediatorRequestManager(configuration, options =>
                {
                    options.DatabaseProvider = "PostgreSQL";
                    options.Schema = schema;
                    options.ConnectionName = "PostgreConnection";
                });
            });

            var logger = resolver.ServiceProvider.GetRequiredService<ILogger<IdentifiedCommandTest>>();

            var context = resolver.ServiceProvider.GetRequiredService<TestContext>();

            var requestManager = resolver.ServiceProvider.GetRequiredService<IRequestManager>(); ;

            var id = Guid.NewGuid();

            var ok = await requestManager.TryCreateRequestForCommandAsync<Request>(id);

            Assert.True(ok);

            await requestManager.TryCompleteRequestAsync<Request>(id, true);

        }

        [IgnoreOnCIFact(DisplayName = "IRequest should handle once"), TestPriority(2)]
        public async Task Request_should_be_handleAsync()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            var schema = CmsSchema;

            resolver.ConfigureServices(services =>
            {

                services.AddSingleton(provider => _testOutput);

                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration(GetType().Assembly);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddMediatR(options =>
                {
                    options.RegisterServicesFromAssemblyContaining<RequestHandler>();
                });

                services.AddDefaultStringIdGenerator();
                services.AddSingleton<SharedService>();

                services.AddEFMediatorRequestManager(configuration, options =>
                {
                    options.DatabaseProvider = "PostgreSQL";
                    options.Schema = schema;
                    options.ConnectionName = "PostgreConnection";
                });
            });

            var logger = resolver.ServiceProvider.GetRequiredService<ILogger<IdentifiedCommandTest>>();
            var request = new Request(Guid.NewGuid());
            var irequest = new IdentifiedCommand<Request>(request, Guid.NewGuid());

            logger.LogInformation("Request Id: {Id}", request.Id);
            Parallel.For(0, 3, async i =>
            {
                using var scope = resolver.ServiceProvider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Send(irequest);
            });
            await Task.Delay(1000);
            var sharedService = resolver.ServiceProvider.GetRequiredService<SharedService>();
            sharedService.HandledServices.Count(x => x == nameof(RequestHandler)).Should().Be(1);
            sharedService.HandledServices.Count(x => x == nameof(RequestIdentifiedCommandHandler)).Should().Be(2);
        }

        [IgnoreOnCIFact(DisplayName = "IRequest<T> should handle once"), TestPriority(2)]
        public async Task Request_with_result_should_be_handleAsync()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            var schema = CmsSchema;

            resolver.ConfigureServices(services =>
            {

                services.AddSingleton(provider => _testOutput);

                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration(GetType().Assembly);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddMediatR(options =>
                {
                    options.RegisterServicesFromAssemblyContaining<RequestHandler>();
                });

                services.AddDefaultStringIdGenerator();
                services.AddSingleton<SharedService>();

                services.AddEFMediatorRequestManager(configuration, options =>
                {
                    options.DatabaseProvider = "PostgreSQL";
                    options.Schema = schema;
                    options.ConnectionName = "PostgreConnection";
                });
            });

            var logger = resolver.ServiceProvider.GetRequiredService<ILogger<IdentifiedCommandTest>>();
            var request = new RequestWithResult(Guid.NewGuid());
            var irequest = new IdentifiedCommand<RequestWithResult, string>(request, Guid.NewGuid());

            logger.LogInformation("Request Id: {Id}", request.Id);
            Parallel.For(0, 3, async i =>
            {
                using var scope = resolver.ServiceProvider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Send(irequest);
            });
            await Task.Delay(1000);
            var sharedService = resolver.ServiceProvider.GetRequiredService<SharedService>();
            sharedService.HandledServices.Count(x => x == nameof(RequestWithResultHandler)).Should().Be(1);
            sharedService.HandledServices.Count(x => x == nameof(RequestWithResultIdentifiedCommandHandler)).Should().Be(2);
        }

        [IgnoreOnCIFact(DisplayName = "IRequest<IOperationResult> should handle once"), TestPriority(2)]
        public async Task Operation_should_be_handleAsync()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            var schema = CmsSchema;

            resolver.ConfigureServices(services =>
            {

                services.AddSingleton(provider => _testOutput);

                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration(GetType().Assembly);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddMediatR(options =>
                {
                    options.RegisterServicesFromAssemblyContaining<RequestHandler>();
                });

                services.AddDefaultStringIdGenerator();
                services.AddSingleton<SharedService>();

                services.AddEFMediatorRequestManager<TestContext>(configuration, options =>
                {
                    options.DatabaseProvider = "PostgreSQL";
                    options.Schema = schema;
                    options.ConnectionName = "PostgreConnection";
                });
            });

            var logger = resolver.ServiceProvider.GetRequiredService<ILogger<IdentifiedCommandTest>>();
            var request = new Operation(Guid.NewGuid());
            var irequest = new IdentifiedCommand<Operation, IOperationResult>(request, Guid.NewGuid());

            logger.LogInformation("Request Id: {Id}", request.Id);
            Parallel.For(0, 3, async i =>
            {
                using var scope = resolver.ServiceProvider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                try
                {
                    await mediator.Send(irequest);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, ex.Message);
                }
            });
            await Task.Delay(1000);
            var sharedService = resolver.ServiceProvider.GetRequiredService<SharedService>();
            sharedService.HandledServices.Count(x => x == nameof(OperationHandler)).Should().Be(1);
            sharedService.HandledServices.Count(x => x == nameof(OperationIdentifiedCommandHandler)).Should().Be(2);
        }

        [IgnoreOnCIFact(DisplayName = "IRequest<IOperationResult<T>> should handle once"), TestPriority(2)]
        public async Task Operation_with_result_should_be_handleAsync()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            var schema = CmsSchema;

            resolver.ConfigureServices(services =>
            {

                services.AddSingleton(provider => _testOutput);

                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration(GetType().Assembly);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddMediatR(options =>
                {
                    options.RegisterServicesFromAssemblyContaining(typeof(IdentifiedCommand<>));
                });

                services.AddDefaultStringIdGenerator();
                services.AddSingleton<SharedService>();

                services.AddIdentifiedCommandHandler<OperationWithResult, IOperationResult<string>, OperationWithResultHandler, OperationWithResultIdentifiedCommandHandler>();

                services.AddEFMediatorRequestManager(configuration, options =>
                {
                    options.DatabaseProvider = "PostgreSQL";
                    options.Schema = schema;
                    options.ConnectionName = "PostgreConnection";
                });
            });

            var logger = resolver.ServiceProvider.GetRequiredService<ILogger<IdentifiedCommandTest>>();
            var request = new OperationWithResult(Guid.NewGuid());
            var irequest = new IdentifiedCommand<OperationWithResult, IOperationResult<string>>(request, Guid.NewGuid());

            logger.LogInformation("Request Id: {Id}", request.Id);
            Parallel.For(0, 3, async i =>
            {
                using var scope = resolver.ServiceProvider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                try
                {
                    await mediator.Send(irequest);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error");
                }
            });
            await Task.Delay(1000);
            var sharedService = resolver.ServiceProvider.GetRequiredService<SharedService>();
            sharedService.HandledServices.Count(x => x == nameof(OperationWithResultHandler)).Should().Be(1);
            sharedService.HandledServices.Count(x => x == nameof(OperationWithResultIdentifiedCommandHandler)).Should().Be(2);
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
            public Task Handle(Request request, CancellationToken cancellationToken)
            {
                _sharedService.HandledServices.Add(nameof(RequestHandler));
                return Task.CompletedTask;
            }
        }

        private class RequestIdentifiedCommandHandler : IdentifiedCommandHandler<Request>
        {
            private readonly SharedService _sharedService;
            public RequestIdentifiedCommandHandler(IMediator mediator, IRequestManager requestManager,
                ILogger<RequestIdentifiedCommandHandler> logger, SharedService sharedService)
                : base(mediator, requestManager, logger)
            {
                _sharedService = sharedService;
            }

            protected override Task<IOperationResult> CreateResultForDuplicatedRequestAsync(Request message)
            {
                _sharedService.HandledServices.Add(nameof(RequestIdentifiedCommandHandler));
                return Task.FromResult((IOperationResult)OperationResult.Success);
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
            public Task<string> Handle(RequestWithResult request, CancellationToken cancellationToken)
            {
                _sharedService.HandledServices.Add(nameof(RequestWithResultHandler));
                return Task.FromResult("Hello World");
            }
        }

        private class RequestWithResultIdentifiedCommandHandler : IdentifiedCommandHandler<RequestWithResult, string>
        {
            private readonly SharedService _sharedService;
            public RequestWithResultIdentifiedCommandHandler(IMediator mediator, IRequestManager requestManager,
                ILogger<RequestWithResultIdentifiedCommandHandler> logger, SharedService sharedService)
                : base(mediator, requestManager, logger)
            {
                _sharedService = sharedService;
            }

            protected override Task<string> CreateResultForDuplicatedRequestAsync(RequestWithResult message)
            {
                _sharedService.HandledServices.Add(nameof(RequestWithResultIdentifiedCommandHandler));
                return Task.FromResult("Duplicated operation");
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

            public Task<IOperationResult> Handle(Operation request, CancellationToken cancellationToken)
            {
                _sharedService.HandledServices.Add(nameof(OperationHandler));
                return Task.FromResult<IOperationResult>(OperationResult.Result("Hello World"));
            }
        }

        private class OperationIdentifiedCommandHandler : IdentifiedCommandHandler<Operation, IOperationResult>
        {
            private readonly SharedService _sharedService;
            public OperationIdentifiedCommandHandler(IMediator mediator, IRequestManager<TestContext> requestManager,
                ILogger<OperationIdentifiedCommandHandler> logger, SharedService sharedService)
                : base(mediator, requestManager, logger)
            {
                _sharedService = sharedService;
            }

            protected override Task<IOperationResult> CreateResultForDuplicatedRequestAsync(Operation message)
            {
                _sharedService.HandledServices.Add(nameof(OperationIdentifiedCommandHandler));
                return Task.FromResult((IOperationResult)OperationResult.Success);
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

            public Task<IOperationResult<string>> Handle(OperationWithResult request, CancellationToken cancellationToken)
            {
                _sharedService.HandledServices.Add(nameof(OperationWithResultHandler));
                return Task.FromResult<IOperationResult<string>>(OperationResult.Result("Hello World"));
            }
        }

        private class OperationWithResultIdentifiedCommandHandler : IdentifiedCommandHandler<OperationWithResult, IOperationResult<string>>
        {
            private readonly SharedService _sharedService;
            public OperationWithResultIdentifiedCommandHandler(IMediator mediator, IRequestManager requestManager,
                ILogger<OperationWithResultIdentifiedCommandHandler> logger, SharedService sharedService)
                : base(mediator, requestManager, logger)
            {
                _sharedService = sharedService;
            }

            protected override Task<IOperationResult<string>> CreateResultForDuplicatedRequestAsync(OperationWithResult message)
            {
                _sharedService.HandledServices.Add(nameof(OperationWithResultIdentifiedCommandHandler));
                return Task.FromResult((IOperationResult<string>)OperationResult.Result("Duplicated operation"));
            }
            protected override (string IdProperty, string CommandId) ExtractDebugInfo(OperationWithResult command)
                => (nameof(command.Id), command.Id.ToString());
        }
        #endregion

        private class SharedService
        {
            public List<string> HandledServices { get; } = new List<string>();
        }
    }
}
