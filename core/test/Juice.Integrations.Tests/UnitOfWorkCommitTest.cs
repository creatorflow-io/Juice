using System;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.EF;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace Juice.Integrations.Tests
{
    /// <summary>
    /// <see cref="UnitOfWork.CommitTransactionAsync"/> reports the failure that happened, never an error from cleaning up after it, wrapped so
    /// that no execution strategy retries it, and never fails a commit that succeeded. Real SQLite.
    /// </summary>
    public class UnitOfWorkCommitTest : IDisposable
    {
        private readonly SqliteConnection _connection;

        public UnitOfWorkCommitTest()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var context = Create();
            context.Database.EnsureCreated();
        }

        public void Dispose() => _connection.Dispose();

        private NoteContext Create(Func<Task>? afterCommit = null, IInterceptor? interceptor = null, Func<ExecutionStrategyDependencies, IExecutionStrategy>? strategy = null)
        {
            var options = new DbContextOptionsBuilder<NoteContext>().UseSqlite(_connection, o =>
            {
                if (strategy is not null)
                {
                    o.ExecutionStrategy(strategy);
                }
            });
            if (interceptor is not null)
            {
                options.AddInterceptors(interceptor);
            }
            return new NoteContext(options.Options, afterCommit);
        }

        private async Task<int> CountAsync(string text)
        {
            await using var check = Create();
            return await check.Notes.CountAsync(n => n.Text == text);
        }

        /// <summary>
        /// The server rejected COMMIT and the provider ended the transaction, as Npgsql does on a deferred constraint or a serialization
        /// failure: the transaction is aborted, and rolling it back fails with "has completed".
        /// </summary>
        [Fact(DisplayName = "A failed commit reports its own error, not the rollback's")]
        public async Task FailedCommitKeepsItsErrorAsync()
        {
            var rejected = new RejectedCommitException();
            await using var context = Create(interceptor: new AbortThenRejectCommit(rejected));
            await using var transaction = await context.Database.BeginTransactionAsync();
            context.Notes.Add(new Note { Text = "rejected" });
            await context.SaveChangesAsync();

            var thrown = await Assert.ThrowsAsync<TransactionCommitFailedException>(() => context.CommitTransactionAsync(transaction.TransactionId));

            thrown.InnerException.Should().BeSameAs(rejected);
            thrown.TransactionId.Should().Be(transaction.TransactionId);
            context.Database.CurrentTransaction.Should().BeNull("a transaction whose rollback failed must not stay current");
            context.HasActiveTransaction.Should().BeFalse();
            (await CountAsync("rejected")).Should().Be(0);
        }

        /// <summary>A cancelled commit stays a cancellation (not retried by any strategy either), and the transaction is let go.</summary>
        [Fact(DisplayName = "A cancelled commit throws OperationCanceledException and releases the transaction")]
        public async Task CancelledCommitStaysACancellationAsync()
        {
            using var cancellation = new CancellationTokenSource();
            await using var context = Create(interceptor: new CancelCommit(cancellation));
            await using var transaction = await context.Database.BeginTransactionAsync();
            context.Notes.Add(new Note { Text = "cancelled" });
            await context.SaveChangesAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => context.CommitTransactionAsync(transaction.TransactionId, cancellation.Token));

            context.Database.CurrentTransaction.Should().BeNull();
            (await CountAsync("cancelled")).Should().Be(0);
        }

        /// <summary>The path production contexts take: DbContextBase adds the commit announcement and the entity reload around the commit.</summary>
        [Fact(DisplayName = "DbContextBase reports a failed commit as the wrapped commit error")]
        public async Task DbContextBaseReportsTheCommitErrorAsync()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            var rejected = new RejectedCommitException();
            BaseNoteContext Open(IInterceptor? interceptor = null)
            {
                var options = new DbContextOptionsBuilder<BaseNoteContext>().UseSqlite(connection);
                if (interceptor is not null)
                {
                    options.AddInterceptors(interceptor);
                }
                return new BaseNoteContext(options.Options);
            }
            await using (var setup = Open())
            {
                await setup.Database.EnsureCreatedAsync();
            }

            await using (var context = Open(new RejectCommit(rejected)))
            {
                await using var transaction = await context.Database.BeginTransactionAsync();
                context.Notes.Add(new Note { Text = "base-rejected" });
                await context.SaveChangesAsync();

                var thrown = await Assert.ThrowsAsync<TransactionCommitFailedException>(() => context.CommitTransactionAsync(transaction.TransactionId));

                thrown.InnerException.Should().BeSameAs(rejected);
                context.HasActiveTransaction.Should().BeFalse();
            }

            await using (var context = Open())
            {
                await using var transaction = await context.Database.BeginTransactionAsync();
                context.Notes.Add(new Note { Text = "base-kept" });
                await context.SaveChangesAsync();

                (await context.CommitTransactionAsync(transaction.TransactionId)).Should().BeTrue();
            }

            await using var check = Open();
            (await check.Notes.CountAsync(n => n.Text == "base-rejected")).Should().Be(0);
            (await check.Notes.CountAsync(n => n.Text == "base-kept")).Should().Be(1);
        }

        /// <summary>The connection broke: the commit fails, and the rollback fails with a provider error of another kind.</summary>
        [Fact(DisplayName = "A failed commit keeps its error whatever the rollback throws")]
        public async Task FailedRollbackOfAnyKindKeepsTheCommitErrorAsync()
        {
            var rejected = new RejectedCommitException();
            await using var context = Create(interceptor: new RejectCommit(rejected, failRollback: true));
            await using var transaction = await context.Database.BeginTransactionAsync();
            context.Notes.Add(new Note { Text = "broken" });
            await context.SaveChangesAsync();

            var thrown = await Assert.ThrowsAsync<TransactionCommitFailedException>(() => context.CommitTransactionAsync(transaction.TransactionId));

            thrown.InnerException.Should().BeSameAs(rejected);
            context.Database.CurrentTransaction.Should().BeNull();
        }

        /// <summary>A commit that fails while the transaction is still open is rolled back, and its error is reported.</summary>
        [Fact(DisplayName = "A commit rejected before completing is rolled back")]
        public async Task OpenFailedCommitIsRolledBackAsync()
        {
            var rejected = new RejectedCommitException();
            await using var context = Create(interceptor: new RejectCommit(rejected));
            await using var transaction = await context.Database.BeginTransactionAsync();
            context.Notes.Add(new Note { Text = "rolled-back" });
            await context.SaveChangesAsync();

            var thrown = await Assert.ThrowsAsync<TransactionCommitFailedException>(() => context.CommitTransactionAsync(transaction.TransactionId));

            thrown.InnerException.Should().BeSameAs(rejected);
            (await CountAsync("rolled-back")).Should().Be(0);
        }

        /// <summary>The commit succeeded, then the post-commit hook failed: the commit is reported as the success it is.</summary>
        [Fact(DisplayName = "A failing post-commit hook does not fail the commit")]
        public async Task FailingHookDoesNotFailTheCommitAsync()
        {
            var hookRuns = 0;
            await using var context = Create(afterCommit: () => { hookRuns++; throw new InvalidOperationException("post-commit hook failed"); });
            await using var transaction = await context.Database.BeginTransactionAsync();
            context.Notes.Add(new Note { Text = "kept" });
            await context.SaveChangesAsync();

            (await context.CommitTransactionAsync(transaction.TransactionId)).Should().BeTrue();

            hookRuns.Should().Be(1);
            (await context.CommitTransactionAsync(transaction.TransactionId)).Should().BeFalse("the transaction is already committed");
            (await CountAsync("kept")).Should().Be(1);
        }

        /// <summary>
        /// The call path TransactionBehavior uses, with a strategy that retries the commit's error. Retrying would re-run SaveChanges over changes
        /// it already accepted, commit an empty transaction and report success; the failure must come out instead.
        /// </summary>
        [Fact(DisplayName = "A retrying execution strategy does not retry a failed commit")]
        public async Task RetryingStrategyDoesNotRetryAFailedCommitAsync()
        {
            var attempts = 0;
            await using var context = Create(interceptor: new RejectCommit(new RejectedCommitException(), once: true),
                strategy: d => new RetryOnRejectedCommit(d));

            var run = () => ResilientTransaction.New(context).ExecuteAsync(async transaction =>
            {
                attempts++;
                if (attempts == 1)
                {
                    context.Notes.Add(new Note { Text = "must-not-vanish" });
                }
                await context.SaveChangesAsync();
                await context.CommitTransactionAsync(transaction.TransactionId);
            });

            var thrown = await Assert.ThrowsAsync<TransactionCommitFailedException>(run);

            thrown.InnerException.Should().BeOfType<RejectedCommitException>();
            attempts.Should().Be(1);
            (await CountAsync("must-not-vanish")).Should().Be(0);
        }

        public sealed class RejectedCommitException : Exception
        {
            public RejectedCommitException() : base("COMMIT rejected by the server") { }
        }

        /// <summary>Treats <see cref="RejectedCommitException"/> as transient, as Npgsql's strategy treats a serialization failure.</summary>
        private sealed class RetryOnRejectedCommit(ExecutionStrategyDependencies dependencies) : ExecutionStrategy(dependencies, 3, TimeSpan.Zero)
        {
            protected override bool ShouldRetryOn(Exception exception) => exception is RejectedCommitException;

            protected override TimeSpan? GetNextDelay(Exception lastException) => base.GetNextDelay(lastException) is null ? null : TimeSpan.Zero;
        }

        /// <summary>Ends the provider's transaction without committing, then fails the commit: what Npgsql does when the server rejects COMMIT.</summary>
        private sealed class AbortThenRejectCommit(Exception rejection) : DbTransactionInterceptor
        {
            public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
                InterceptionResult result, CancellationToken cancellationToken = default)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw rejection;
            }
        }

        private sealed class CancelCommit(CancellationTokenSource cancellation) : DbTransactionInterceptor
        {
            public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
                InterceptionResult result, CancellationToken cancellationToken = default)
            {
                cancellation.Cancel();
                throw new OperationCanceledException(cancellation.Token);
            }
        }

        public sealed class BaseNoteContext(DbContextOptions<BaseNoteContext> options) : DbContextBase(options)
        {
            public DbSet<Note> Notes => Set<Note>();

            protected override void ConfigureModel(ModelBuilder modelBuilder) => modelBuilder.Entity<Note>().ToTable("Notes");
        }

        private sealed class RejectCommit(Exception rejection, bool failRollback = false, bool once = false) : DbTransactionInterceptor
        {
            private int _rejected;

            public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
                InterceptionResult result, CancellationToken cancellationToken = default)
            {
                if (once && Interlocked.Increment(ref _rejected) > 1)
                {
                    return ValueTask.FromResult(result);
                }
                throw rejection;
            }

            public override ValueTask<InterceptionResult> TransactionRollingBackAsync(DbTransaction transaction, TransactionEventData eventData,
                InterceptionResult result, CancellationToken cancellationToken = default)
                => failRollback ? throw new IOException("Exception while writing to stream") : ValueTask.FromResult(result);
        }

        public sealed class Note
        {
            public int Id { get; set; }
            public string Text { get; set; } = "";
        }

        public sealed class NoteContext(DbContextOptions<NoteContext> options, Func<Task>? afterCommit) : UnitOfWork(options)
        {
            public DbSet<Note> Notes => Set<Note>();

            protected override Task OnTransactionCommittedAsync() => afterCommit?.Invoke() ?? Task.CompletedTask;
        }
    }
}
