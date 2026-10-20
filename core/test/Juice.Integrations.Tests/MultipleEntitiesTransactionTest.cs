using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.Domain;
using Juice.EF;
using Juice.MediatR;
using Juice.MediatR.Behaviors;
using Juice.Messaging;
using Juice.Messaging.Outbox;
using Juice.Messaging.Outbox.EF;
using Juice.Messaging.Policies;
using Juice.XUnit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Juice.Integrations.Tests
{
    /// <summary>
    /// Verifies domain event -> TransactionBehavior -> outbox when several aggregates are
    /// added within the same request scope. Runs against in-memory SQLite (no infrastructure)
    /// using the real <see cref="TransactionBehavior{TRequest, TResponse, TContext}"/>,
    /// <c>OutboxEventService</c> and <c>OutboxRepository</c>.
    /// </summary>
    [InitializeMessageContext]
    public class MultipleEntitiesTransactionTest : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;

        public MultipleEntitiesTransactionTest(ITestOutputHelper output)
        {
            _output = output;
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            _provider = BuildServices();

            using var scope = _provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<TicketContext>().Database.EnsureCreated();
        }

        public void Dispose()
        {
            _provider.Dispose();
            _connection.Dispose();
        }

        [Fact(DisplayName = "One command adding N aggregates writes N outbox events in one transaction")]
        public async Task SingleCommand_MultipleAggregates_AllEventsInOutboxAsync()
        {
            using (var scope = _provider.CreateScope())
            {
                var count = await scope.ServiceProvider.GetRequiredService<IMediator>()
                    .Send(new CreateTicketsCommand(5));
                count.Should().Be(5);
            }

            using (var scope = _provider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<TicketContext>();
                var tickets = await context.Tickets.ToListAsync();
                var outbox = await context.Outbox.Include(e => e.Deliveries).ToListAsync();

                tickets.Should().HaveCount(5);
                outbox.Should().HaveCount(5);
                outbox.Select(e => e.EventId).Should().OnlyHaveUniqueItems();
                outbox.Select(e => e.TransactionId).Distinct().Should().ContainSingle()
                    .Which.Should().NotBeNullOrEmpty();
                outbox.Should().AllSatisfy(e =>
                {
                    e.EventTypeName.Should().Be(nameof(TicketCreatedIntegrationEvent));
                    e.Deliveries.Should().ContainSingle()
                        .Which.State.Should().Be(DeliveryState.NotPublished);
                });

                var recorder = _provider.GetRequiredService<Recorder>();
                recorder.IntegrationEvents.Select(e => e.TicketId)
                    .Should().BeEquivalentTo(tickets.Select(t => t.Id));
                outbox.Select(e => e.EventId)
                    .Should().BeEquivalentTo(recorder.IntegrationEvents.Select(e => e.MessageId));
            }
        }

        [Fact(DisplayName = "Sequential commands in the same scope each commit their own outbox events")]
        public async Task SequentialCommands_SameScope_NoDuplicatesAsync()
        {
            using (var scope = _provider.CreateScope())
            {
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Send(new CreateTicketsCommand(2));
                await mediator.Send(new CreateTicketsCommand(3));
            }

            using (var scope = _provider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<TicketContext>();
                (await context.Tickets.CountAsync()).Should().Be(5);
                var outbox = await context.Outbox.ToListAsync();
                outbox.Should().HaveCount(5);
                outbox.Select(e => e.EventId).Should().OnlyHaveUniqueItems();
                outbox.GroupBy(e => e.TransactionId).Select(g => g.Count())
                    .Should().BeEquivalentTo([2, 3]);
            }
        }

        [Fact(DisplayName = "Aggregate added by a domain event handler also gets its event into the outbox")]
        public async Task DomainEventHandler_AddsAggregate_CascadedEventInOutboxAsync()
        {
            using (var scope = _provider.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<IMediator>()
                    .Send(new CreateTicketsCommand(2, SpawnFollowUp: true));
            }

            using (var scope = _provider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<TicketContext>();
                var tickets = await context.Tickets.ToListAsync();
                var outbox = await context.Outbox.ToListAsync();
                _output.WriteLine($"Tickets: {tickets.Count}, outbox events: {outbox.Count}");

                tickets.Should().HaveCount(4, "2 root tickets + 2 follow-ups added by the domain event handler");
                outbox.Should().HaveCount(4, "every persisted aggregate's domain event should reach the outbox");
            }
        }

        [Fact(DisplayName = "Failure while dispatching one aggregate's event rolls back all aggregates and outbox")]
        public async Task HandlerFailure_RollsBackEverythingAsync()
        {
            using (var scope = _provider.CreateScope())
            {
                var send = async () => await scope.ServiceProvider.GetRequiredService<IMediator>()
                    .Send(new CreateTicketsCommand(3, FailOnIndex: 2));
                await send.Should().ThrowAsync<InvalidOperationException>();
            }

            using (var scope = _provider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<TicketContext>();
                (await context.Tickets.CountAsync()).Should().Be(0);
                (await context.Outbox.CountAsync()).Should().Be(0);
                (await context.OutboxDeliveries.CountAsync()).Should().Be(0);
            }
        }

        [Fact(DisplayName = "Nested command does not commit the outer command's aggregates early")]
        public async Task NestedCommand_OuterFailure_RollsBackAllAsync()
        {
            using (var scope = _provider.CreateScope())
            {
                var send = async () => await scope.ServiceProvider.GetRequiredService<IMediator>()
                    .Send(new CreateTicketsWithNestedCommand(OuterCount: 2, InnerCount: 2, FailAfterInner: true));
                await send.Should().ThrowAsync<InvalidOperationException>();
            }

            using (var scope = _provider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<TicketContext>();
                var tickets = await context.Tickets.CountAsync();
                var outbox = await context.Outbox.CountAsync();
                _output.WriteLine($"Tickets: {tickets}, outbox events: {outbox}");

                tickets.Should().Be(0, "the outer command failed, so nothing it added should be committed");
                outbox.Should().Be(0);
            }
        }

        [Fact(DisplayName = "Context is released after a command so later commands open their own transaction")]
        public async Task Command_ReleasesManagedContextAsync()
        {
            using var scope = _provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TicketContext>();

            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new CreateTicketsCommand(1));
            context.IsManaged.Should().BeFalse();

            var send = async () => await scope.ServiceProvider.GetRequiredService<IMediator>()
                .Send(new CreateTicketsCommand(1, FailOnIndex: 0));
            await send.Should().ThrowAsync<InvalidOperationException>();
            context.IsManaged.Should().BeFalse("the context must be released even when the command fails");
        }

        [Fact(DisplayName = "Repository writes inside a command commit together with the outbox")]
        public async Task Repository_InsideCommand_AllEventsInOutboxAsync()
        {
            using (var scope = _provider.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<IMediator>()
                    .Send(new RepositoryTicketsCommand(3));
            }

            using (var scope = _provider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<TicketContext>();
                (await context.Tickets.CountAsync()).Should().Be(3);
                var outbox = await context.Outbox.ToListAsync();
                outbox.Should().HaveCount(3);
                outbox.Select(e => e.TransactionId).Distinct().Should().ContainSingle();
            }
        }

        [Fact(DisplayName = "Repository writes inside a failing command are rolled back")]
        public async Task Repository_InsideFailingCommand_RollsBackAsync()
        {
            using (var scope = _provider.CreateScope())
            {
                var send = async () => await scope.ServiceProvider.GetRequiredService<IMediator>()
                    .Send(new RepositoryTicketsCommand(3, FailAfterWrites: true));
                await send.Should().ThrowAsync<InvalidOperationException>();
            }

            using (var scope = _provider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<TicketContext>();
                (await context.Tickets.CountAsync()).Should().Be(0, "repository writes are staged while the command is managed");
                (await context.Outbox.CountAsync()).Should().Be(0);
            }
        }

        [Fact(DisplayName = "UnitOfWork Update/Delete inside a failing command are rolled back")]
        public async Task UnitOfWorkUpdateDelete_InsideFailingCommand_RollsBackAsync()
        {
            Guid keepId, deleteId;
            using (var scope = _provider.CreateScope())
            {
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Send(new CreateTicketsCommand(2));
                var context = scope.ServiceProvider.GetRequiredService<TicketContext>();
                var ids = await context.Tickets.OrderBy(t => t.Title).Select(t => t.Id).ToListAsync();
                (keepId, deleteId) = (ids[0], ids[1]);

                var send = async () => await mediator.Send(new UpdateDeleteTicketsCommand(keepId, "Renamed", deleteId));
                await send.Should().ThrowAsync<InvalidOperationException>();
            }

            using (var scope = _provider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<TicketContext>();
                var tickets = await context.Tickets.ToListAsync();
                tickets.Select(t => t.Id).Should().BeEquivalentTo([keepId, deleteId], "the delete must not be committed");
                tickets.Single(t => t.Id == keepId).Title.Should().NotBe("Renamed", "the update must not be committed");
            }
        }

        [Fact(DisplayName = "Repository writes outside a command still save immediately")]
        public async Task Repository_OutsideCommand_SavesImmediatelyAsync()
        {
            using (var scope = _provider.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<TicketRepository>();
                var added = await repository.AddAsync(new Ticket("Standalone"));
                added.Succeeded.Should().BeTrue();
            }

            using (var scope = _provider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<TicketContext>();
                (await context.Tickets.CountAsync()).Should().Be(1);
            }
        }

        private ServiceProvider BuildServices()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddTestOutputLogger(_output).SetMinimumLevel(LogLevel.Information));

            var options = new DbContextOptionsBuilder<TicketContext>().UseSqlite(_connection).Options;
            services.AddScoped(sp => new TicketContext(sp, options));

            services.AddMessaging().AddOutbox();
            services.AddSingleton<IMessagePublishingPolicy>(new FixedRoutePolicy());
            services.AddSingleton<Recorder>();
            services.AddScoped<TicketRepository>();

            services.AddMediatR(cfg => cfg.AddOpenBehavior(typeof(TicketTransactionBehavior<,>)));
            services.AddTransient<IRequestHandler<CreateTicketsCommand, int>, CreateTicketsCommandHandler>();
            services.AddTransient<IRequestHandler<CreateTicketsWithNestedCommand, int>, CreateTicketsWithNestedCommandHandler>();
            services.AddTransient<IRequestHandler<RepositoryTicketsCommand, int>, RepositoryTicketsCommandHandler>();
            services.AddTransient<IRequestHandler<UpdateDeleteTicketsCommand, int>, UpdateDeleteTicketsCommandHandler>();
            services.AddTransient<INotificationHandler<TicketCreatedDomainEvent>, TicketCreatedDomainEventHandler>();

            return services.BuildServiceProvider();
        }

        #region Domain

        public class Ticket : AggregateRoot<INotification>
        {
            private Ticket() { }

            public Ticket(string title, bool isFollowUp = false)
            {
                Id = Guid.NewGuid();
                Title = title;
                IsFollowUp = isFollowUp;
                this.AddDomainEvent(new TicketCreatedDomainEvent(this));
            }

            public Guid Id { get; private set; }
            public string Title { get; private set; } = default!;

            public void Rename(string title) => Title = title;
            public bool IsFollowUp { get; private set; }

            // Test-only switches carried on the aggregate so the domain event handler can act on them
            [System.ComponentModel.DataAnnotations.Schema.NotMapped]
            public bool SpawnFollowUp { get; set; }
            [System.ComponentModel.DataAnnotations.Schema.NotMapped]
            public bool FailOnDispatch { get; set; }
        }

        public record TicketCreatedDomainEvent(Ticket Ticket) : MessageBase, INotification;

        public record TicketCreatedIntegrationEvent(Guid TicketId, string Title) : IntegrationEvent;

        public class TicketContext : DbContextBase, IOutboxContext
        {
            public TicketContext(IServiceProvider serviceProvider, DbContextOptions<TicketContext> options)
                : base(options)
            {
                ConfigureServices(serviceProvider);
            }

            public DbSet<Ticket> Tickets { get; set; } = default!;
            public DbSet<OutboxEvent> Outbox { get; set; } = default!;
            public DbSet<OutboxDelivery> OutboxDeliveries { get; set; } = default!;

            protected override void ConfigureModel(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<Ticket>(entity =>
                {
                    entity.HasKey(t => t.Id);
                    entity.Ignore(t => t.DomainEvents);
                    entity.Ignore(t => t.ValidationErrors);
                });
                this.ConfigureOutbox(modelBuilder);
            }
        }

        #endregion

        #region Application

        public interface ITicketCommand { }

        public record CreateTicketsCommand(int Count, bool SpawnFollowUp = false, int FailOnIndex = -1)
            : MessageBase, IRequest<int>, ITicketCommand;

        public record CreateTicketsWithNestedCommand(int OuterCount, int InnerCount, bool FailAfterInner)
            : MessageBase, IRequest<int>, ITicketCommand;

        public class TicketTransactionBehavior<TRequest, TResponse>
            : TransactionBehavior<TRequest, TResponse, TicketContext>
            where TRequest : IRequest<TResponse>, ITicketCommand
        {
            public TicketTransactionBehavior(TicketContext dbContext,
                IOutboxService<TicketContext> outboxService,
                IMediator mediator,
                ILogger<TicketTransactionBehavior<TRequest, TResponse>> logger)
                : base(dbContext, outboxService, mediator, logger)
            {
            }
        }

        public class CreateTicketsCommandHandler : IRequestHandler<CreateTicketsCommand, int>
        {
            private readonly TicketContext _context;
            public CreateTicketsCommandHandler(TicketContext context) => _context = context;

            public async ValueTask<int> Handle(CreateTicketsCommand request, CancellationToken cancellationToken)
            {
                for (var i = 0; i < request.Count; i++)
                {
                    var ticket = new Ticket($"Ticket {i}")
                    {
                        SpawnFollowUp = request.SpawnFollowUp,
                        FailOnDispatch = i == request.FailOnIndex
                    };
                    await ((IUnitOfWork)_context).AddAsync(ticket, cancellationToken);
                }
                return request.Count;
            }
        }

        public class CreateTicketsWithNestedCommandHandler : IRequestHandler<CreateTicketsWithNestedCommand, int>
        {
            private readonly TicketContext _context;
            private readonly IMediator _mediator;
            public CreateTicketsWithNestedCommandHandler(TicketContext context, IMediator mediator)
            {
                _context = context;
                _mediator = mediator;
            }

            public async ValueTask<int> Handle(CreateTicketsWithNestedCommand request, CancellationToken cancellationToken)
            {
                for (var i = 0; i < request.OuterCount; i++)
                {
                    await ((IUnitOfWork)_context).AddAsync(new Ticket($"Outer {i}"), cancellationToken);
                }
                await _mediator.Send(new CreateTicketsCommand(request.InnerCount), cancellationToken);
                if (request.FailAfterInner)
                {
                    throw new InvalidOperationException("Outer command failed after inner command");
                }
                return request.OuterCount + request.InnerCount;
            }
        }

        public record RepositoryTicketsCommand(int Count, bool FailAfterWrites = false)
            : MessageBase, IRequest<int>, ITicketCommand;

        public record UpdateDeleteTicketsCommand(Guid RenameId, string NewTitle, Guid DeleteId)
            : MessageBase, IRequest<int>, ITicketCommand;

        public class TicketRepository : RepositoryBase<Ticket, TicketContext>
        {
            public TicketRepository(TicketContext context) : base(context)
            {
            }
        }

        public class RepositoryTicketsCommandHandler : IRequestHandler<RepositoryTicketsCommand, int>
        {
            private readonly TicketRepository _repository;
            public RepositoryTicketsCommandHandler(TicketRepository repository) => _repository = repository;

            public async ValueTask<int> Handle(RepositoryTicketsCommand request, CancellationToken cancellationToken)
            {
                for (var i = 0; i < request.Count; i++)
                {
                    var added = await _repository.AddAsync(new Ticket($"Repository {i}"), cancellationToken);
                    added.Succeeded.Should().BeTrue();
                }
                if (request.FailAfterWrites)
                {
                    throw new InvalidOperationException("Command failed after repository writes");
                }
                return request.Count;
            }
        }

        public class UpdateDeleteTicketsCommandHandler : IRequestHandler<UpdateDeleteTicketsCommand, int>
        {
            private readonly TicketContext _context;
            public UpdateDeleteTicketsCommandHandler(TicketContext context) => _context = context;

            public async ValueTask<int> Handle(UpdateDeleteTicketsCommand request, CancellationToken cancellationToken)
            {
                IUnitOfWork uow = _context;
                var renamed = await uow.FindAsync<Ticket>(t => t.Id == request.RenameId, cancellationToken);
                renamed!.Rename(request.NewTitle);
                (await _context.UpdateAsync(renamed, cancellationToken)).Succeeded.Should().BeTrue();

                var deleted = await uow.FindAsync<Ticket>(t => t.Id == request.DeleteId, cancellationToken);
                (await uow.DeleteAsync(deleted!, cancellationToken)).Succeeded.Should().BeTrue();

                throw new InvalidOperationException("Command failed after update and delete");
            }
        }

        public class TicketCreatedDomainEventHandler : INotificationHandler<TicketCreatedDomainEvent>
        {
            private readonly IOutboxService<TicketContext> _outbox;
            private readonly TicketContext _context;
            private readonly Recorder _recorder;

            public TicketCreatedDomainEventHandler(IOutboxService<TicketContext> outbox, TicketContext context, Recorder recorder)
            {
                _outbox = outbox;
                _context = context;
                _recorder = recorder;
            }

            public async ValueTask Handle(TicketCreatedDomainEvent notification, CancellationToken cancellationToken = default)
            {
                var ticket = notification.Ticket;
                if (ticket.FailOnDispatch)
                {
                    throw new InvalidOperationException($"Dispatch failed for {ticket.Title}");
                }
                if (ticket.SpawnFollowUp && !ticket.IsFollowUp)
                {
                    await ((IUnitOfWork)_context).AddAsync(new Ticket($"{ticket.Title} follow-up", isFollowUp: true), cancellationToken);
                }

                var integrationEvent = new TicketCreatedIntegrationEvent(ticket.Id, ticket.Title);
                _recorder.IntegrationEvents.Add(integrationEvent);
                await _outbox.AddEventAsync(integrationEvent);
            }
        }

        public class Recorder
        {
            public ConcurrentBag<TicketCreatedIntegrationEvent> IntegrationEvents { get; } = [];
        }

        private sealed class FixedRoutePolicy : IMessagePublishingPolicy
        {
            public ValueTask<IReadOnlyCollection<PublishRoute>> ResolveAsync(PolicyResolveContext context)
            {
                IReadOnlyCollection<PublishRoute> routes = [new PublishRoute("rabbitmq", "x.tickets")];
                return ValueTask.FromResult(routes);
            }
        }

        #endregion
    }
}
