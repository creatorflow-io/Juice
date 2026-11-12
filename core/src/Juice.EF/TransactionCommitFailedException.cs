namespace Juice.EF
{
    /// <summary>
    /// Committing a transaction failed. The provider's error is the <see cref="Exception.InnerException"/>: inspect it to tell a conflict
    /// (a unique or deferred constraint, a serialization failure) from a lost connection.
    /// </summary>
    /// <remarks>
    /// The failure is wrapped so that an execution strategy never retries it. A retrying strategy re-runs only the work inside
    /// <see cref="ResilientTransaction"/>, after <c>SaveChanges</c> has already accepted the changes, so a retry would commit an empty
    /// transaction and report success.
    /// <para>
    /// The outcome is not always "not committed". When the connection broke during <c>COMMIT</c>, or something failed after the database
    /// committed (a transaction interceptor, closing the connection), the transaction may have committed after all. Before running the whole
    /// command again, make sure a second run is harmless (an idempotency key, a unique constraint).
    /// </para>
    /// </remarks>
    public sealed class TransactionCommitFailedException : Exception
    {
        public TransactionCommitFailedException(Guid transactionId, Exception innerException)
            : base($"Committing transaction {transactionId} failed: {innerException.Message}", innerException)
        {
            TransactionId = transactionId;
        }

        public Guid TransactionId { get; }
    }
}
