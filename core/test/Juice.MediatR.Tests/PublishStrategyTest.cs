using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Juice.MediatR.Tests
{
    /// <summary>How a notification's handlers run: in parallel by default, one after another when the mediator says so or the notification does.</summary>
    public class PublishStrategyTest
    {
        public sealed class Tracker
        {
            private int _running;
            public int MostAtOnce;
            public ConcurrentQueue<string> Order { get; } = new();

            public async Task RunAsync(string name)
            {
                var now = Interlocked.Increment(ref _running);
                int seen;
                while ((seen = Volatile.Read(ref MostAtOnce)) < now && Interlocked.CompareExchange(ref MostAtOnce, now, seen) != seen)
                {
                }
                await Task.Delay(50);
                Order.Enqueue(name);
                Interlocked.Decrement(ref _running);
            }
        }

        public sealed record Unmarked : MessageBase, INotification;

        public sealed record Marked : MessageBase, ISequenceNotification;

        public sealed class FirstHandler(Tracker tracker) : INotificationHandler<Unmarked>, INotificationHandler<Marked>
        {
            public async ValueTask Handle(Unmarked notification, CancellationToken cancellationToken) => await tracker.RunAsync("first");
            public async ValueTask Handle(Marked notification, CancellationToken cancellationToken) => await tracker.RunAsync("first");
        }

        public sealed class SecondHandler(Tracker tracker) : INotificationHandler<Unmarked>, INotificationHandler<Marked>
        {
            public async ValueTask Handle(Unmarked notification, CancellationToken cancellationToken) => await tracker.RunAsync("second");
            public async ValueTask Handle(Marked notification, CancellationToken cancellationToken) => await tracker.RunAsync("second");
        }

        private static (IMediator Mediator, Tracker Tracker) Build(NotificationPublishStrategy? strategy)
        {
            var services = new ServiceCollection();
            services.AddSingleton<Tracker>();
            services.AddMediatR(builder =>
            {
                if (strategy is { } s)
                {
                    builder.UseDefaultPublishStrategy(s);
                }
            });
            services.AddTransient<INotificationHandler<Unmarked>, FirstHandler>();
            services.AddTransient<INotificationHandler<Unmarked>, SecondHandler>();
            services.AddTransient<INotificationHandler<Marked>, FirstHandler>();
            services.AddTransient<INotificationHandler<Marked>, SecondHandler>();
            var provider = services.BuildServiceProvider();
            return (provider.GetRequiredService<IMediator>(), provider.GetRequiredService<Tracker>());
        }

        [Fact]
        public async Task Unmarked_notification_handlers_run_in_parallel_by_defaultAsync()
        {
            var (mediator, tracker) = Build(strategy: null);
            await mediator.Publish(new Unmarked());
            Assert.Equal(2, tracker.MostAtOnce);
        }

        [Fact]
        public async Task Unmarked_notification_handlers_run_one_after_another_when_the_mediator_is_sequentialAsync()
        {
            var (mediator, tracker) = Build(NotificationPublishStrategy.Sequential);
            await mediator.Publish(new Unmarked());
            Assert.Equal(1, tracker.MostAtOnce);
            Assert.Equal(["first", "second"], tracker.Order);
        }

        [Fact]
        public async Task A_sequence_notification_runs_one_after_another_whatever_the_defaultAsync()
        {
            var (mediator, tracker) = Build(NotificationPublishStrategy.Parallel);
            await mediator.Publish(new Marked());
            Assert.Equal(1, tracker.MostAtOnce);
            Assert.Equal(["first", "second"], tracker.Order);
        }
    }
}
