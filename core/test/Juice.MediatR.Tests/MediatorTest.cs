using System;
using System.Collections.Generic;
using System.Diagnostics;
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
                builder.RegisterServicesFromAssemblyContaining<MediatorTest>();
            });
            services.AddTransient<IPipelineBehavior<Request>, TimingBehavior<Request>>();
            services.AddTransient<IPipelineBehavior<Ping, string>, TimingBehavior<Ping, string>>();
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

            var handlers = provider.GetServices<IRequestHandler<Request>>();
            handlers.Should().HaveCount(1);

            await mediator.Send(new Request());
            var shared = provider.GetRequiredService<SharedService>();
            shared.Calls.Should().Contain("RequestHandler");
            shared.Clear();
            // parallel
            Parallel.For(0, 10000, async i =>
            {
                await mediator.Send(new Request());
            });

            await WaitAsync(shared, 10000);
            shared.CallCount.Should().Be(10000);
        }

        [Fact]
        public async Task Request_should_responseAsync()
        {
            var provider = BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();
            Assert.NotNull(mediator);
            Assert.IsType<MediatR.Internal.Mediator>(mediator);
            var behaviors = provider.GetServices<IPipelineBehavior<Ping, string>>();
            var response = await mediator.Send(new Ping());
            response.Should().Be("Pong");
            var shared = provider.GetRequiredService<SharedService>();
            shared.Clear();

            _output.WriteLine("Sending 10,000 requests...");

            // parallel
            Parallel.For(0, 10000, async i =>
            {
                await mediator.Send(new Ping());
            });
            await WaitAsync(shared, 10000);
            shared.CallCount.Should().Be(10000);
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
            var mediator = serviceProvider.GetRequiredService<IMediator>();
            var shared = serviceProvider.GetRequiredService<SharedService>();

            for (var i = 0; i < 100; i++)
            {
                await TestAsync(mediator, shared);
            }
        }

        private async Task TestAsync(IMediator mediator, SharedService shared)
        {
            shared.Clear();
            var start = Stopwatch.StartNew();
            long before = GC.GetAllocatedBytesForCurrentThread();
            int n = 100000;
            Parallel.For(0, n, async (i) =>
            {
                await mediator.Publish(new Notification());
            });
            await WaitAsync(shared, n);
            long after = GC.GetAllocatedBytesForCurrentThread();
            start.Stop();
            _output.WriteLine("Allocated " + (after - before) / 1024 + " kB for " + n + " requests. Taken " + start.ElapsedMilliseconds + " ms");
            shared.CallCount.Should().Be(n);
            GC.Collect();
        }

        private async Task WaitAsync(SharedService shared, int count)
        {
            var start = Stopwatch.StartNew();
            while (shared.CallCount < count)
            {
                await Task.Delay(10);
                if (start.ElapsedMilliseconds > 3000)
                {
                    break;
                }
            }
        }

        private class SharedService
        {
            public HashSet<string> Calls { get; } = new();
            private int _count;
            private object _lock = new();
            public int CallCount
            {
                get
                {
                    lock (_lock)
                    {
                        return _count;
                    }
                }
            }
            public void Increment()
            {
                lock (_lock)
                {
                    _count++;
                }
            }
            public void Clear()
            {
                lock (_lock)
                {
                    _count = 0;
                }
            }
        }

        private class Request : IRequest
        {
        }
        private class RequestHandler : IRequestHandler<Request>
        {
            private readonly SharedService _shared;
            public RequestHandler(SharedService shared) => _shared = shared;
            public async ValueTask Handle(Request request, CancellationToken cancellationToken)
            {
                _shared.Calls.Add("RequestHandler");
                _shared.Increment();
                await Task.Delay(100, cancellationToken);
            }
        }
        private class TimingBehavior<TRequest> : IPipelineBehavior<TRequest>
    where TRequest : IRequest
        {
            public int Order => int.MaxValue - 20; // run late
            private readonly ILogger _logger;
            public TimingBehavior(ILogger<TimingBehavior<TRequest>> logger) => _logger = logger;
            public async ValueTask Handle(TRequest request, RequestHandlerDelegate<TRequest> next, CancellationToken ct)
            {
                var sw = Stopwatch.StartNew();
                await next.Invoke(request, ct).ConfigureAwait(false);
                sw.Stop();
                _logger.LogInformation("Handled {Request} in {Elapsed} ticks", typeof(TRequest).Name, sw.ElapsedTicks);
            }
        }
        private class Ping : IRequest<string>
        {
        }
        private class PingHandler(SharedService shared) : IRequestHandler<Ping, string>
        {
            public ValueTask<string> Handle(Ping request, CancellationToken cancellationToken)
            {
                shared.Increment();
                return ValueTask.FromResult("Pong");
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
        private class Notification : INotification { }
        private class NotificationHandler(SharedService sharedService) : INotificationHandler<Notification>
        {
            public ValueTask Handle(Notification notification, CancellationToken cancellationToken = default)
            {
                sharedService.Increment();
                return ValueTask.CompletedTask;
            }
        }
    }
}
