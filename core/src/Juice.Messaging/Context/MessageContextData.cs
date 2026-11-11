
namespace Juice.Messaging.Context
{
    public sealed record MessageContextData(
        string CorrelationId,
        string? CausationId,
        string ExecutionId,
        string Source
    )
    {
        /// <summary>
        /// Who published the message being handled, as the broker vouched for it (RabbitMQ's validated <c>user_id</c>), or null when the
        /// transport doesn't say. Unlike a header, a publisher can't set it to someone else, within the broker's own limits: users with the
        /// <c>impersonator</c> tag, and federation links with <c>trust-user-id</c>, aren't checked. A copy a consumer republished
        /// (Juice's retries and parking) has none.
        /// </summary>
        public string? PublisherId { get; init; }
    }
}
