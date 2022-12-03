namespace Juice.MediatR.RequestManager.EF
{
    public class ClientRequest
    {
        /// <summary>
        /// For EFCore binding
        /// </summary>
        private ClientRequest() { }

        public ClientRequest(Guid id, string name)
        {
            Id = id;
            Name = name;
            Time = DateTimeOffset.Now;
            State = RequestState.New;
        }
        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public DateTimeOffset Time { get; private set; }
        public RequestState State { get; private set; }
        public DateTimeOffset? CompletedTime { get; private set; }

        public void MarkAsDone()
        {
            State = RequestState.Processed;
            CompletedTime = DateTimeOffset.Now;
        }

        public void MarkAsFailed()
        {
            State = RequestState.ProcessedFailed;
            CompletedTime = DateTimeOffset.Now;
        }
    }
}
