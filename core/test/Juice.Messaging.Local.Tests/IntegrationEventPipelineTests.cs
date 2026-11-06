using FluentAssertions;
using Juice.Messaging.Integrations;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Messaging.Local.Tests
{
    /// <summary>
    /// <see cref="IIntegrationEventPipelineBehavior"/>s run around an event's handlers, in their DI scope, after the
    /// inbox check.
    /// </summary>
    public class IntegrationEventPipelineTests
    {
        [Fact]
        public async Task Behaviors_run_by_order_around_the_handlers_Async()
        {
            // Registered inner first: Order decides, not registration.
            using var provider = Build(s => s
                .AddIntegrationEventPipelineBehavior<InnerBehavior>()
                .AddIntegrationEventPipelineBehavior<OuterBehavior>()
                .AddIntegrationEventPipelineBehavior<OuterBehavior>());

            var result = await DispatchAsync(provider, new PipelineEvent(), typeof(RecordingHandler));

            result.Should().Be(EventDispatchResult.Success);
            provider.GetRequiredService<Journal>().Entries.Should().Equal(
                "outer before", "inner before", "handler", "inner after Success", "outer after Success");
        }

        [Fact]
        public async Task Behaviors_share_the_handlers_scope_Async()
        {
            using var provider = Build(s => s.AddIntegrationEventPipelineBehavior<OuterBehavior>());

            await DispatchAsync(provider, new PipelineEvent(), typeof(RecordingHandler));

            var journal = provider.GetRequiredService<Journal>();
            journal.BehaviorScopes.Should().ContainSingle();
            journal.HandlerScopes.Should().Equal(journal.BehaviorScopes);
        }

        [Fact]
        public async Task Behavior_sees_a_failed_handler_and_its_exception_Async()
        {
            using var provider = Build(s => s.AddIntegrationEventPipelineBehavior<OuterBehavior>());

            var result = await DispatchAsync(provider, new PipelineEvent(), typeof(FailingHandler));

            result.Should().Be(EventDispatchResult.Failure);
            var journal = provider.GetRequiredService<Journal>();
            journal.Entries.Should().Equal("outer before", "outer after Failure");
            journal.Errors.Should().ContainSingle().Which.Message.Should().Be("boom");
        }

        [Theory]
        [InlineData(typeof(RecordingHandler), typeof(FailingHandler))]
        [InlineData(typeof(FailingHandler), typeof(RecordingHandler))]
        public async Task One_failed_handler_fails_the_event_even_when_another_succeeds_Async(Type first, Type second)
        {
            using var provider = Build(_ => { });

            var result = await provider.GetRequiredService<IntegrationEventDispatcher>().DispatchAsync(new PipelineEvent(),
                new EventDispatchContext([first, second], nameof(PipelineEvent), null, "test"));

            result.Should().Be(EventDispatchResult.Failure);
            provider.GetRequiredService<Journal>().Entries.Should().Contain("handler", "the handler that can succeed still runs");
        }

        [Fact]
        public async Task A_handler_runs_inside_the_message_dispatch_scope_Async()
        {
            MessageDispatchScope.IsActive.Should().BeFalse();
            using var provider = Build(s => s.AddScoped<ScopeCheckingHandler>());

            await DispatchAsync(provider, new PipelineEvent(), typeof(ScopeCheckingHandler));

            ScopeCheckingHandler.Seen.Should().BeTrue();
            MessageDispatchScope.IsActive.Should().BeFalse("the mark ends with the handler");
        }

        private sealed class ScopeCheckingHandler : IIntegrationEventHandler<PipelineEvent>
        {
            public static bool Seen;

            public Task HandleAsync(PipelineEvent @event)
            {
                Seen = MessageDispatchScope.IsActive;
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task Behavior_gets_the_dispatch_cancellation_token_Async()
        {
            using var provider = Build(s => s.AddIntegrationEventPipelineBehavior<OuterBehavior>());
            using var cts = new CancellationTokenSource();

            await provider.GetRequiredService<IntegrationEventDispatcher>().DispatchAsync(new PipelineEvent(),
                new EventDispatchContext([typeof(RecordingHandler)], nameof(PipelineEvent), null, "test"), cts.Token);

            provider.GetRequiredService<Journal>().Tokens.Should().Equal(cts.Token);
        }

        [Fact]
        public async Task Duplicate_does_not_reach_the_behaviors_Async()
        {
            using var provider = Build(s => s.AddIntegrationEventPipelineBehavior<OuterBehavior>());
            var evt = new PipelineEvent();

            await DispatchAsync(provider, evt, typeof(RecordingHandler));
            var second = await DispatchAsync(provider, evt, typeof(RecordingHandler));

            second.Should().Be(EventDispatchResult.Duplicated);
            provider.GetRequiredService<Journal>().Entries.Should().Equal(
                "outer before", "handler", "outer after Success");
        }

        [Fact]
        public async Task A_dispatch_still_running_is_not_reported_as_completed_Async()
        {
            using var provider = Build(s => s.AddIntegrationEventPipelineBehavior<OuterBehavior>());
            var evt = new PipelineEvent();
            using var scope = provider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<Juice.Messaging.Idempotency.IIdempotencyService>()
                .TryBeginRequestAsync(nameof(PipelineEvent), $"test:{evt.MessageId}");

            var second = await DispatchAsync(provider, evt, typeof(RecordingHandler));

            second.Should().Be(EventDispatchResult.InProgress);
            provider.GetRequiredService<Journal>().Entries.Should().BeEmpty();
        }

        private static ServiceProvider Build(Action<IServiceCollection> configure)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            // Shared across the dispatcher's scopes, so a second delivery is a duplicate.
            services.AddDistributedMemoryCache();
            services.AddMessaging().AddIdempotencyDistributedCache();
            services.AddSingleton<Journal>();
            services.AddScoped<ScopeMarker>();
            services.AddScoped<RecordingHandler>();
            services.AddScoped<FailingHandler>();
            configure(services);
            return services.BuildServiceProvider();
        }

        private static Task<EventDispatchResult> DispatchAsync(IServiceProvider provider, PipelineEvent evt, Type handler)
            => provider.GetRequiredService<IntegrationEventDispatcher>()
                .DispatchAsync(evt, new EventDispatchContext([handler], nameof(PipelineEvent), null, "test"));

        private sealed record PipelineEvent : IntegrationEvent;

        private sealed class ScopeMarker;

        private sealed class Journal
        {
            public List<string> Entries { get; } = new();
            public List<ScopeMarker> BehaviorScopes { get; } = new();
            public List<ScopeMarker> HandlerScopes { get; } = new();
            public List<Exception> Errors { get; } = new();
            public List<CancellationToken> Tokens { get; } = new();
        }

        private abstract class JournalBehavior(Journal journal, ScopeMarker scope, string name, int order)
            : IIntegrationEventPipelineBehavior
        {
            public int Order => order;

            public async ValueTask<EventDispatchResult> Handle(IIntegrationEvent evt, EventDispatchContext context,
                IntegrationEventHandlerDelegate next, CancellationToken cancellationToken)
            {
                journal.Entries.Add(name + " before");
                journal.BehaviorScopes.Add(scope);
                journal.Tokens.Add(cancellationToken);
                var result = await next(evt, context, cancellationToken);
                journal.Entries.Add($"{name} after {result}");
                journal.Errors.AddRange(context.HandlerErrors);
                return result;
            }
        }

        private sealed class OuterBehavior(Journal journal, ScopeMarker scope) : JournalBehavior(journal, scope, "outer", 0);

        private sealed class InnerBehavior(Journal journal, ScopeMarker scope) : JournalBehavior(journal, scope, "inner", 10);

        private sealed class RecordingHandler(Journal journal, ScopeMarker scope) : IIntegrationEventHandler<PipelineEvent>
        {
            public Task HandleAsync(PipelineEvent @event)
            {
                journal.Entries.Add("handler");
                journal.HandlerScopes.Add(scope);
                return Task.CompletedTask;
            }
        }

        private sealed class FailingHandler : IIntegrationEventHandler<PipelineEvent>
        {
            public Task HandleAsync(PipelineEvent @event) => throw new InvalidOperationException("boom");
        }
    }
}
