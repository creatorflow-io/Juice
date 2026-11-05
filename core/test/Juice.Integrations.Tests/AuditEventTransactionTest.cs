using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.Domain.Events;
using Juice.EF;
using Juice.EF.Extensions;
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
    /// What an audit consumer needs to report only durable changes: audit events published inside a managed transaction
    /// carry its id, and its commit is announced. Real <see cref="TransactionBehavior{TRequest, TResponse, TContext}"/>
    /// and SQLite transactions.
    /// </summary>
    [InitializeMessageContext]
    public class AuditEventTransactionTest : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;

        public AuditEventTransactionTest()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            _provider = BuildServices();

            using var scope = _provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<NoteContext>().Database.EnsureCreated();
        }

        public void Dispose()
        {
            _provider.Dispose();
            _connection.Dispose();
        }

        private List<object> Events => _provider.GetRequiredService<Recorder>().Events;

        [Fact(DisplayName = "A committed command's audit events carry the transaction id, and its commit is announced after them")]
        public async Task Committed_command_announces_its_transaction_Async()
        {
            using var scope = _provider.CreateScope();

            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new AddNotes(2));

            Events.Should().HaveCount(3);
            var changes = Events.Take(2).Cast<AuditEvent>().ToList();
            changes.Select(e => e.TransactionId).Should().OnlyContain(id => id != null);
            changes.Select(e => e.TransactionId).Distinct().Should().ContainSingle();
            Events[2].Should().BeOfType<TransactionCommitted>()
                .Which.TransactionId.Should().Be(changes[0].TransactionId!.Value);
        }

        [Fact(DisplayName = "A commit that fails announces nothing, and the data is not saved")]
        public async Task Failed_commit_is_not_announced_Async()
        {
            using (var scope = _provider.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<NoteContext>().FailCommit = true;

                var send = async () => await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new AddNotes(2));

                await send.Should().ThrowAsync<InvalidOperationException>();
            }

            Events.Should().HaveCount(2).And.OnlyContain(e => e is AuditEvent);
            Events.Cast<AuditEvent>().Should().OnlyContain(e => e.TransactionId != null);
            using var check = _provider.CreateScope();
            (await check.ServiceProvider.GetRequiredService<NoteContext>().Notes.CountAsync()).Should().Be(0);
        }

        [Fact(DisplayName = "Sequential commands announce their own transactions")]
        public async Task Sequential_commands_announce_their_own_transactions_Async()
        {
            using var scope = _provider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            await mediator.Send(new AddNotes(1));
            await mediator.Send(new AddNotes(1));

            Events.OfType<TransactionCommitted>().Select(e => e.TransactionId)
                .Should().Equal(Events.OfType<AuditEvent>().Select(e => e.TransactionId!.Value));
            Events.OfType<TransactionCommitted>().Select(e => e.TransactionId).Should().OnlyHaveUniqueItems();
        }

        [Fact(DisplayName = "A plain save has no transaction to wait for")]
        public async Task Plain_save_is_not_stamped_Async()
        {
            using var scope = _provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NoteContext>();

            context.Notes.Add(new Note(Guid.NewGuid()));
            await context.SaveChangesAsync();

            Events.Should().ContainSingle().Which.Should().BeAssignableTo<AuditEvent>()
                .Which.TransactionId.Should().BeNull();
        }

        [Fact(DisplayName = "A plain save that fails reports no change, and the next save reports only its own")]
        public async Task Failed_plain_save_reports_nothing_Async()
        {
            var id = Guid.NewGuid();
            using (var scope = _provider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<NoteContext>();
                context.Notes.Add(new Note(id));
                await context.SaveChangesAsync();
            }
            Events.Clear();

            using (var scope = _provider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<NoteContext>();
                context.Notes.Add(new Note(id));

                var save = async () => await context.SaveChangesAsync();

                await save.Should().ThrowAsync<DbUpdateException>();
                Events.Should().BeEmpty();

                context.ChangeTracker.Clear();
                context.Notes.Add(new Note(Guid.NewGuid()));
                await context.SaveChangesAsync();
                Events.Should().ContainSingle();
            }
        }

        private ServiceProvider BuildServices()
        {
            var services = new ServiceCollection();
            services.AddLogging();

            var options = new DbContextOptionsBuilder<NoteContext>().UseSqlite(_connection).Options;
            services.AddScoped(sp => new NoteContext(sp, options));

            services.AddMessaging().AddOutbox();
            services.AddSingleton<IMessagePublishingPolicy>(new FixedRoutePolicy());
            services.AddSingleton<Recorder>();

            services.AddMediatR(cfg => cfg.AddOpenBehavior(typeof(NoteTransactionBehavior<,>)));
            services.AddTransient<IRequestHandler<AddNotes, int>, AddNotesHandler>();
            services.AddTransient<INotificationHandler<AuditEvent<Note>>, RecordingHandler>();
            services.AddTransient<INotificationHandler<TransactionCommitted>, RecordingHandler>();

            return services.BuildServiceProvider();
        }

        public class Note
        {
            private Note() { }
            public Note(Guid id) => Id = id;
            public Guid Id { get; private set; }
        }

        public class NoteContext : DbContextBase, IOutboxContext
        {
            public NoteContext(IServiceProvider serviceProvider, DbContextOptions<NoteContext> options)
                : base(options)
            {
                ConfigureServices(serviceProvider);
            }

            public bool FailCommit { get; set; }

            public DbSet<Note> Notes { get; set; } = default!;
            public DbSet<OutboxEvent> Outbox { get; set; } = default!;
            public DbSet<OutboxDelivery> OutboxDeliveries { get; set; } = default!;

            public override Task<bool> CommitTransactionAsync(Guid transactionId, CancellationToken token = default)
                => FailCommit
                    ? throw new InvalidOperationException("commit failed")
                    : base.CommitTransactionAsync(transactionId, token);

            protected override void ConfigureModel(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<Note>(entity =>
                {
                    entity.HasKey(n => n.Id);
                    entity.IsAuditable();
                });
                this.ConfigureOutbox(modelBuilder);
            }
        }

        public interface INoteCommand { }

        public record AddNotes(int Count) : MessageBase, IRequest<int>, INoteCommand;

        public class NoteTransactionBehavior<TRequest, TResponse>
            : TransactionBehavior<TRequest, TResponse, NoteContext>
            where TRequest : IRequest<TResponse>, INoteCommand
        {
            public NoteTransactionBehavior(NoteContext dbContext, IOutboxService<NoteContext> outboxService,
                IMediator mediator, ILogger<NoteTransactionBehavior<TRequest, TResponse>> logger)
                : base(dbContext, outboxService, mediator, logger)
            {
            }
        }

        public class AddNotesHandler : IRequestHandler<AddNotes, int>
        {
            private readonly NoteContext _context;
            public AddNotesHandler(NoteContext context) => _context = context;

            public ValueTask<int> Handle(AddNotes request, CancellationToken cancellationToken)
            {
                for (var i = 0; i < request.Count; i++)
                {
                    _context.Notes.Add(new Note(Guid.NewGuid()));
                }
                return ValueTask.FromResult(request.Count);
            }
        }

        public class RecordingHandler : INotificationHandler<AuditEvent<Note>>, INotificationHandler<TransactionCommitted>
        {
            private readonly Recorder _recorder;
            public RecordingHandler(Recorder recorder) => _recorder = recorder;

            public ValueTask Handle(AuditEvent<Note> notification, CancellationToken cancellationToken = default)
            {
                _recorder.Events.Add(notification);
                return ValueTask.CompletedTask;
            }

            public ValueTask Handle(TransactionCommitted notification, CancellationToken cancellationToken = default)
            {
                _recorder.Events.Add(notification);
                return ValueTask.CompletedTask;
            }
        }

        public class Recorder
        {
            public List<object> Events { get; } = [];
        }

        private sealed class FixedRoutePolicy : IMessagePublishingPolicy
        {
            public ValueTask<IReadOnlyCollection<PublishRoute>> ResolveAsync(PolicyResolveContext context)
            {
                IReadOnlyCollection<PublishRoute> routes = [new PublishRoute("rabbitmq", "x.notes")];
                return ValueTask.FromResult(routes);
            }
        }
    }
}
