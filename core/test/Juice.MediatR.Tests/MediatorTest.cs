using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Juice.MediatR.Tests
{
    public class MediatorTest
    {
        private readonly ITestOutputHelper _output;
        public MediatorTest(ITestOutputHelper output)
        {
            _output = output;
        }

        private IServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();
            services.AddLogging(options =>
            {
                options.SetMinimumLevel(LogLevel.Debug);
                options.AddTestOutputLogger();
            });
            services.AddMediatR(builder =>
            {
                builder.RegisterServicesFromAssemblyContaining<MediatorTest>(true);
            });
            //services.AddTransient<IPipelineBehavior<Request>, TimingBehavior<Request>>();
            //services.AddTransient<IPipelineBehavior<Ping, string>, TimingBehavior<Ping, string>>();
            services.AddSingleton(_output);
            services.AddSingleton<SharedService>();
            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task Request_should_sendAsync()
        {
            var provider = BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();
            Assert.NotNull(mediator);
            Assert.IsType<Internal.Mediator>(mediator);

            var shared = provider.GetRequiredService<SharedService>();

            // parallel
            var n = 10000;
            Parallel.For(0, n, async i =>
            {
                using var scope = provider.CreateScope();
                var m = scope.ServiceProvider.GetRequiredService<IMediator>();
                await m.Send(new Request());
            });

            await WaitAsync(shared, n);
            shared.CallCount.Should().Be(n);
            shared.BehaviorCount.Should().Be(n);
            _output.WriteLine("Completed {0} requests", n);
        }

        [Fact]
        public async Task Request_should_responseAsync()
        {
            var provider = BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();
            Assert.NotNull(mediator);
            Assert.IsType<Internal.Mediator>(mediator);
            var behaviors = provider.GetServices<IPipelineBehavior<Ping, string>>();
            var response = await mediator.Send(new Cmd<Ping>());
            response.Should().Be("Pong");
            var shared = provider.GetRequiredService<SharedService>();
            shared.Clear();

            _output.WriteLine("Sending 10,000 requests...");

            // parallel
            var n = 10000;
            Parallel.For(0, n, async i =>
            {
                using var scope = provider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Send(new Cmd<Ping>());
            });
            await WaitAsync(shared, n);
            shared.CallCount.Should().Be(n);
        }

        [Fact]
        public async Task StreamRequest_should_responseAsync()
        {
            var provider = BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();
            Assert.NotNull(mediator);
            Assert.IsType<Internal.Mediator>(mediator);
            var behaviors = provider.GetServices<IStreamPipelineBehavior<GetNumbers, int>>();
            behaviors.Should().HaveCount(1);
            var stream = mediator.CreateStream(new GetNumbers(10));
            var list = new List<int>();
            await foreach (var item in stream)
            {
                list.Add(item);
            }
            list.Should().HaveCount(10);
            list.Should().ContainInOrder(0, 1, 2, 3, 4, 5, 6, 7, 8, 9);
            // parallel
            _output.WriteLine("Sending 1,000 stream requests...");
            var tasks = new List<Task>();
            for (int j = 0; j < 1000; j++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var s = mediator.CreateStream(new GetNumbers(10));
                    var l = new List<int>();
                    await foreach (var item in s)
                    {
                        l.Add(item);
                    }
                    l.Should().HaveCount(10);
                    l.Should().ContainInOrder(0, 1, 2, 3, 4, 5, 6, 7, 8, 9);
                }));
            }
            await Task.WhenAll(tasks);
        }

        [Fact]
        public async Task Notification_should_sendAsync()
        {
            var serviceProvider = BuildServiceProvider();
            var shared = serviceProvider.GetRequiredService<SharedService>();

            using var scope = serviceProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await NotificationAsync(mediator, shared);
        }

        private async Task NotificationAsync(IMediator mediator, SharedService shared)
        {
            shared.Clear();
            shared.ClearBehavior();
            var start = Stopwatch.StartNew();
            long before = GC.GetAllocatedBytesForCurrentThread();
            int n = "true".Equals(Environment.GetEnvironmentVariable("CI")) ? 10 : 100000;
            Parallel.For(0, n, async (i) =>
            {
                await mediator.Publish(new Notification());
            });

            _output.WriteLine("Published " + n + " messages. Taken " + start.ElapsedMilliseconds + " ms");
            await WaitAsync(shared, n);

            long after = GC.GetAllocatedBytesForCurrentThread();
            start.Stop();
            _output.WriteLine("Allocated " + (after - before) / 1024 + " kB for " + n + " requests. Taken " + start.ElapsedMilliseconds + " ms");
            shared.CallCount.Should().Be(n);
            shared.BehaviorCount.Should().Be(n);
            GC.Collect();
        }

        [Fact]
        public async Task FireAndForgetNotification_should_sendAsync()
        {
            var serviceProvider = BuildServiceProvider();
            var shared = serviceProvider.GetRequiredService<SharedService>();
            using var scope = serviceProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await FireAndForgetNotificationAsync(mediator, shared);
        }

        private async Task FireAndForgetNotificationAsync(IMediator mediator, SharedService shared)
        {
            shared.Clear();
            shared.ClearBehavior();
            var start = Stopwatch.StartNew();
            long before = GC.GetAllocatedBytesForCurrentThread();
            int n = "true".Equals(Environment.GetEnvironmentVariable("CI")) ? 10 : 10000;
            for (var i = 0; i < n; i++)
            {
                await mediator.Publish(new FireAndForgetNotification());
            }
            _output.WriteLine("Published " + n + " messages. Taken " + start.ElapsedMilliseconds + " ms");
            start.ElapsedMilliseconds.Should().BeLessThan(500); // should be quick as same as parallel publish, fire and forget
            await WaitAsync(shared, n, 5000);

            long after = GC.GetAllocatedBytesForCurrentThread();
            start.Stop();
            _output.WriteLine("Allocated " + (after - before) / 1024 + " kB for " + n + " requests. Taken " + start.ElapsedMilliseconds + " ms");
            shared.CallCount.Should().Be(n);
            shared.BehaviorCount.Should().Be(n);
            GC.Collect();
        }

        private async Task WaitAsync(SharedService shared, int count, int ms = 30000)
        {
            var start = Stopwatch.StartNew();
            while (shared.CallCount < count)
            {
                await Task.Delay(10);
                if (start.ElapsedMilliseconds > ms)
                {
                    break;
                }
            }
        }
        #region Request
        private class Request : IRequest
        {
        }
        private class RequestHandler : IRequestHandler<Request>
        {
            private readonly SharedService _shared;
            private Guid _id = Guid.NewGuid();
            private ILogger _logger;
            public RequestHandler(SharedService shared, ILogger<RequestHandler> logger)
            {
                _shared = shared;
                _logger = logger;
            }

            public async ValueTask Handle(Request request, CancellationToken cancellationToken)
            {
                await Task.Delay(100, cancellationToken);
                // increment call count must be the last operation to avoid breaking wait logic
                _shared.Increment();
                _logger.LogInformation("{Id} Handled Request", _id);
            }
        }
        private class TimingBehavior<TRequest> : IPipelineBehavior<TRequest>
            where TRequest : IRequest
        {
            public int Order => int.MaxValue - 20; // run late
            private readonly ILogger _logger;
            private Guid _id = Guid.NewGuid();
            private readonly SharedService _shared;
            public TimingBehavior(ILogger<TimingBehavior<TRequest>> logger, SharedService shared)
            {
                _logger = logger;
                _shared = shared;
            }
            public async ValueTask Handle(TRequest request, RequestHandlerDelegate<TRequest> next, CancellationToken ct)
            {
                var sw = Stopwatch.StartNew();
                await next.Invoke(request, ct).ConfigureAwait(false);
                sw.Stop();
                _shared.IncrementBehavior();
                _logger.LogInformation("{Id} Handled {Request} in {Elapsed} ticks", _id, typeof(TRequest).Name, sw.ElapsedTicks);
            }
        }
        #endregion
        #region Request/Response
        private abstract class MyTask
        {
            public abstract string Response { get; set; }
        }
        private class Cmd<TTask> : IRequest<string>
            where TTask : MyTask, new()
        {
            public TTask Task { get; set; } = new TTask();
        }
        private abstract class CmdHandler<TTask> : IRequestHandler<Cmd<TTask>, string>
            where TTask : MyTask, new()
        {
            public virtual ValueTask<string> Handle(Cmd<TTask> request, CancellationToken cancellationToken)
            {
                return ValueTask.FromResult(request.Task.Response);
            }
        }
        private class Ping: MyTask
        {
            public override string Response { get; set; } = "Pong";
        }
        private class PingCommandHandler(SharedService shared) : CmdHandler<Ping>
        {
            public override ValueTask<string> Handle(Cmd<Ping> request, CancellationToken cancellationToken)
            {
                shared.Increment();
                return base.Handle(request, cancellationToken);
            }
        }
        private class TimingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
        {
            public int Order => int.MaxValue - 20; // run late
            private readonly ILogger _logger;
            public TimingBehavior(ILogger<TimingBehavior<TRequest, TResponse>> logger) => _logger = logger;
            public async ValueTask<TResponse> Handle(TRequest request, RequestHandlerDelegate<TRequest, TResponse> next, CancellationToken ct)
            {
                var sw = Stopwatch.StartNew();
                var res = await next.Invoke(request, ct).ConfigureAwait(false);
                sw.Stop();
                _logger.LogInformation("Handled {Request} in {Elapsed} ticks", typeof(TRequest).Name, sw.ElapsedTicks);
                return res;
            }
        }
        // Behavior 2: simple retry (for reliability)
        private class RetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
            where TRequest : IRequest<TResponse>
        {
            public int Order => int.MaxValue - 10; // run late
            private readonly int _retries;
            public RetryBehavior(int retries = 2)
            {
                _retries = retries;
            }


            public async ValueTask<TResponse> Handle(TRequest request, RequestHandlerDelegate<TRequest, TResponse> next, CancellationToken ct)
            {
                int attempt = 0;
                while (true)
                {
                    try
                    {
                        var res = await next.Invoke(request, ct).ConfigureAwait(false);
                        return res;
                    }
                    catch when (attempt++ < _retries && !ct.IsCancellationRequested)
                    {
                        // backoff (cheap): spin or small delay without allocation
                        await Task.Delay(TimeSpan.FromMilliseconds(10 * attempt), ct).ConfigureAwait(false);
                    }
                }
            }
        }
        #endregion
        #region Stream
        private record GetNumbers(int Count) : IStreamRequest<int>;
        private class GetNumbersHandler : IStreamRequestHandler<GetNumbers, int>
        {
            public async IAsyncEnumerable<int> Handle(GetNumbers request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
            {
                for (int i = 0; i < request.Count; i++)
                {
                    yield return i;
                    await Task.Delay(1, cancellationToken);
                }
            }
        }
        private class LoggingBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
            where TRequest : IStreamRequest<TResponse>
        {
            public int Order => int.MaxValue - 20; // run late
            private readonly ILogger _logger;
            public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger) => _logger = logger;
            public async IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerDelegate<TRequest, TResponse> next, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
            {
                var sw = Stopwatch.StartNew();
                await foreach (var item in next.Invoke(request, ct).ConfigureAwait(false))
                {
                    _logger.LogDebug("Streaming {Request} item {item} at {Elapsed} ticks", typeof(TRequest).Name, item, sw.ElapsedTicks);
                    yield return item;
                }
                sw.Stop();
                _logger.LogInformation("Handled stream {Request} in {Elapsed} ticks", typeof(TRequest).Name, sw.ElapsedTicks);
            }
        }
        #endregion
        #region Notification
        private class Notification : INotification { }
        private class NotificationHandler(SharedService sharedService) : INotificationHandler<Notification>
        {
            public async ValueTask Handle(Notification notification, CancellationToken cancellationToken = default)
            {
                await Task.Delay(50, cancellationToken);
                sharedService.Increment();
            }
        }
        private class NotificationBehavior<TNotification> : INotificationPipelineBehavior<TNotification>
            where TNotification : INotification
        {
            public int Order => int.MaxValue - 20; // run late
            private readonly SharedService _shared;
            public NotificationBehavior(SharedService shared)
            {
                _shared = shared;
            }
            public async ValueTask Handle(TNotification notification, NotificationHandlerDelegate<TNotification> next, CancellationToken ct)
            {
                await next.Invoke(notification, ct).ConfigureAwait(false);
                _shared.IncrementBehavior();
            }
        }
        private record FireAndForgetNotification : IFireAndForgetNotification
        {
        }
        private class FireAndForgetNotificationHandler(SharedService sharedService) : INotificationHandler<FireAndForgetNotification>
        {
            public async ValueTask Handle(FireAndForgetNotification notification, CancellationToken cancellationToken = default)
            {
                await Task.Delay(50, cancellationToken);
                sharedService.Increment();
            }
        }
        #endregion
    }
}
