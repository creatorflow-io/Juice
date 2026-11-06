using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.Messaging.Integrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Juice.MediatR.Tests
{
    /// <summary>A returned failure is replayed to an HTTP caller's repeat, but a message's retry has to run the command again.</summary>
    public class IdempotencyFailedResultTest
    {
        private static ServiceProvider Build()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssemblyContaining<FlakyCommandHandler>();
                cfg.AddIdempotencyRequestBehavior();
            });
            services.AddDistributedMemoryCache();
            services.AddMessaging().AddIdempotencyDistributedCache();
            services.AddSingleton<FlakyCommandHandler.Calls>();
            return services.BuildServiceProvider();
        }

        private static async Task<IOperationResult> SendAsync(IServiceProvider provider, string key)
        {
            using var scope = provider.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new FlakyCommand(key), CancellationToken.None);
        }

        [Fact]
        public async Task A_retry_inside_a_message_handler_runs_the_command_againAsync()
        {
            using var provider = Build();
            var key = Guid.NewGuid().ToString();

            using (MessageDispatchScope.Enter())
            {
                (await SendAsync(provider, key)).Succeeded.Should().BeFalse();
                (await SendAsync(provider, key)).Succeeded.Should().BeTrue("the retry ran the command again");
            }

            provider.GetRequiredService<FlakyCommandHandler.Calls>().Count.Should().Be(2);
        }

        [Fact]
        public async Task A_repeat_outside_a_message_handler_gets_the_same_failureAsync()
        {
            using var provider = Build();
            var key = Guid.NewGuid().ToString();

            (await SendAsync(provider, key)).Succeeded.Should().BeFalse();
            (await SendAsync(provider, key)).Succeeded.Should().BeFalse("the stored answer is replayed");

            provider.GetRequiredService<FlakyCommandHandler.Calls>().Count.Should().Be(1);
        }

        [Fact]
        public async Task A_success_inside_a_message_handler_is_not_run_twiceAsync()
        {
            using var provider = Build();
            provider.GetRequiredService<FlakyCommandHandler.Calls>().Count = 1; // the next call succeeds
            var key = Guid.NewGuid().ToString();

            using (MessageDispatchScope.Enter())
            {
                await SendAsync(provider, key);
                await SendAsync(provider, key);
            }

            provider.GetRequiredService<FlakyCommandHandler.Calls>().Count.Should().Be(2);
        }
    }

    public record FlakyCommand(string IdempotencyKey) : MessageBase, IRequest<IOperationResult>, IIdempotentRequest;

    /// <summary>Fails the first time it's called.</summary>
    public class FlakyCommandHandler(FlakyCommandHandler.Calls calls) : IRequestHandler<FlakyCommand, IOperationResult>
    {
        public sealed class Calls { public int Count; }

        public ValueTask<IOperationResult> Handle(FlakyCommand request, CancellationToken cancellationToken)
            => new(++calls.Count == 1 ? OperationResult.Failed("not yet") : OperationResult.Success);
    }
}
