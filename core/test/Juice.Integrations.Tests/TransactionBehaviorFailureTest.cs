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
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Juice.Integrations.Tests
{
    /// <summary>
    /// A command that fails leaves nothing for the next command in its scope to save or publish (#126), and a retrying execution
    /// strategy never replays <see cref="TransactionBehavior{TRequest, TResponse, TContext}"/>'s work after its first save (#127).
    /// Real SQLite; the failures are thrown by a save interceptor, the n-th save of the context.
    /// </summary>
    [InitializeMessageContext]
    public class TransactionBehaviorFailureTest : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;
        private readonly FailingSaves _saves = new();

        public TransactionBehaviorFailureTest()
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

        // The behavior's first save is the context's save #1, the outbox save #2 (no handler changes entities).
        private const int FirstSave = 1;
        private const int OutboxSave = 2;

        [Theory(DisplayName = "The next command in the scope saves and publishes only its own work after one fails")]
        [InlineData(FirstSave)]
        [InlineData(OutboxSave)]
        public async Task Failed_command_leaves_nothing_for_the_next_Async(int failingSave)
        {
            using var scope = _provider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            _saves.Fail(failingSave, () => new InvalidOperationException("save failed"));
            var failing = new AddNote(Guid.NewGuid());
            var send = async () => await mediator.Send(failing);
            await send.Should().ThrowAsync<InvalidOperationException>().WithMessage("save failed");

            _saves.Reset();
            Events.Clear();
            var next = new AddNote(Guid.NewGuid());
            await mediator.Send(next);

            (await NoteIdsAsync()).Should().Equal(next.Id);
            (await OutboxEventIdsAsync()).Should().Equal(next.EventId);
            var committed = Events.OfType<TransactionCommitted>().Should().ContainSingle().Subject;
            Events.OfType<AuditEvent<Note>>().Should().ContainSingle()
                .Which.TransactionId.Should().Be(committed.TransactionId);
        }

        [Fact(DisplayName = "The next command in the scope saves only its own work after a handler fails")]
        public async Task Failed_handler_leaves_nothing_for_the_next_Async()
        {
            using var scope = _provider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var send = async () => await mediator.Send(new AddNote(Guid.NewGuid(), FailInHandler: true));
            await send.Should().ThrowAsync<InvalidOperationException>().WithMessage("handler failed");

            var next = new AddNote(Guid.NewGuid());
            await mediator.Send(next);

            (await NoteIdsAsync()).Should().Equal(next.Id);
            (await OutboxEventIdsAsync()).Should().Equal(next.EventId);
            Events.OfType<AuditEvent<Note>>().Should().ContainSingle();
        }

        private async Task<List<Guid>> NoteIdsAsync()
        {
            using var scope = _provider.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<NoteContext>().Notes.Select(n => n.Id).ToListAsync();
        }

        private async Task<List<Guid>> OutboxEventIdsAsync()
        {
            using var scope = _provider.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<NoteContext>().Outbox.Select(e => e.EventId).ToListAsync();
        }

        private ServiceProvider BuildServices()
        {
            var services = new ServiceCollection();
            services.AddLogging();

            var options = new DbContextOptionsBuilder<NoteContext>()
                .UseSqlite(_connection, o => o.ExecutionStrategy(d => new RetryOnTransient(d)))
                .AddInterceptors(_saves)
                .Options;
            services.AddScoped(sp => new NoteContext(sp, options));

            services.AddMessaging().AddOutbox();
            services.AddSingleton<IMessagePublishingPolicy>(new FixedRoutePolicy());
            services.AddSingleton<Recorder>();

            services.AddMediatR(cfg => cfg.AddOpenBehavior(typeof(NoteTransactionBehavior<,>)));
            services.AddTransient<IRequestHandler<AddNote, Guid>, AddNoteHandler>();
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

            public DbSet<Note> Notes { get; set; } = default!;
            public DbSet<OutboxEvent> Outbox { get; set; } = default!;
            public DbSet<OutboxDelivery> OutboxDeliveries { get; set; } = default!;

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

        public record AddNote(Guid Id, bool FailInHandler = false) : MessageBase, IRequest<Guid>, INoteCommand
        {
            public Guid EventId { get; } = Guid.NewGuid();
        }

        public record NoteAdded : IntegrationEvent
        {
            public Guid NoteId { get; init; }
        }

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

        public class AddNoteHandler : IRequestHandler<AddNote, Guid>
        {
            private readonly NoteContext _context;
            private readonly IOutboxService<NoteContext> _outbox;

            public AddNoteHandler(NoteContext context, IOutboxService<NoteContext> outbox)
            {
                _context = context;
                _outbox = outbox;
            }

            public async ValueTask<Guid> Handle(AddNote request, CancellationToken cancellationToken)
            {
                _context.Notes.Add(new Note(request.Id));
                await _outbox.AddEventAsync(new NoteAdded { MessageId = request.EventId, NoteId = request.Id });
                if (request.FailInHandler)
                {
                    throw new InvalidOperationException("handler failed");
                }
                return request.Id;
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

        /// <summary>Counts the saves of the context and fails the configured one.</summary>
        private sealed class FailingSaves : SaveChangesInterceptor
        {
            private int _failOn;
            private Func<Exception>? _failure;

            public int Calls { get; private set; }

            public void Fail(int call, Func<Exception> failure)
            {
                Calls = 0;
                _failOn = call;
                _failure = failure;
            }

            public void Reset()
            {
                Calls = 0;
                _failure = null;
            }

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
                InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                Calls++;
                if (_failure != null && Calls == _failOn)
                {
                    throw _failure();
                }
                return ValueTask.FromResult(result);
            }
        }

        private sealed class TransientFailure() : Exception("transient");

        private sealed class RetryOnTransient(ExecutionStrategyDependencies dependencies) : ExecutionStrategy(dependencies, 3, TimeSpan.Zero)
        {
            protected override bool ShouldRetryOn(Exception exception) => exception is TransientFailure;
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
