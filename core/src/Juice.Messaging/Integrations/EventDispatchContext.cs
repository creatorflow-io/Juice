namespace Juice.Messaging.Integrations
{
    public sealed record EventDispatchContext(
        IEnumerable<Type> Handlers,
        string EventName,
        string? TenantId,
        string Source)
    {
        private readonly List<Exception> _handlerErrors = new();

        /// <summary>
        /// The exceptions thrown by the event's handlers in this dispatch, which the dispatcher catches and reports as
        /// <see cref="EventDispatchResult.Failure"/>. For <see cref="IIntegrationEventPipelineBehavior"/>s.
        /// </summary>
        public IReadOnlyList<Exception> HandlerErrors => _handlerErrors;

        internal void AddHandlerError(Exception exception) => _handlerErrors.Add(exception);
    }
}
