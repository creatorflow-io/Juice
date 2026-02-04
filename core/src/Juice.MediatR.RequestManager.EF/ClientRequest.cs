namespace Juice.MediatR.RequestManager.EF
{
    public class ClientRequest
    {
        /// <summary>
        /// For EFCore binding
        /// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        private ClientRequest() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        public ClientRequest(Guid id, string name)
        {
            Id = id;
            Name = name;
            Time = DateTimeOffset.Now;
            State = RequestState.New;
        }
        public Guid Id { get; private set; }
        public string Name { get; set; }
        public DateTimeOffset Time { get; private set; }
        public RequestState State { get; private set; }
        public DateTimeOffset? CompletedTime { get; private set; }
        public string? Result { get; private set; }
    }
}
