namespace Juice.Messaging.Idempotency.EF
{
    public class IdempotencyRecord
    {
        /// <summary>
        /// For EFCore binding
        /// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        private IdempotencyRecord() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        public IdempotencyRecord(string scope, string key)
        {
            Scope = scope;
            Key = key;
            CreatedAt = DateTimeOffset.Now;
            State = RequestState.New;
        }
        public string Key { get; private set; }
        public string Scope { get; set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public RequestState State { get; private set; }
        public DateTimeOffset? CompletedAt { get; private set; }
        public string? Result { get; private set; }
    }
}
