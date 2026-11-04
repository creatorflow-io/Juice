namespace Juice.Messaging.Integrations
{
    /// <summary>
    /// Runs the next behavior, or the event's handlers after the last one.
    /// </summary>
    public delegate ValueTask<EventDispatchResult> IntegrationEventHandlerDelegate(IIntegrationEvent evt,
        EventDispatchContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Pipeline behavior (around advice) for the handlers of one integration event, as <c>IPipelineBehavior</c> is for
    /// a mediator request. It runs in the DI scope the handlers are resolved from, so it shares scoped services with
    /// them (an audit scope, for example).
    /// <para>Behaviors run by <see cref="Order"/>, the lowest outermost, after the inbox check (a duplicate never
    /// reaches them) and after the event's tenant is resolved. <see cref="System.Diagnostics.Activity.Current"/> is the
    /// message's consumer span, so the trace the message was created in is available.</para>
    /// <para>Handler exceptions are caught by the dispatcher, reported as <see cref="EventDispatchResult.Failure"/> and
    /// listed in <see cref="EventDispatchContext.HandlerErrors"/>. An exception thrown by a behavior fails the delivery
    /// like any dispatch error, so a behavior that only observes should catch its own.</para>
    /// <para>Register with <c>services.AddIntegrationEventPipelineBehavior&lt;TBehavior&gt;()</c>.</para>
    /// </summary>
    public interface IIntegrationEventPipelineBehavior
    {
        int Order { get; }

        ValueTask<EventDispatchResult> Handle(IIntegrationEvent evt, EventDispatchContext context,
            IntegrationEventHandlerDelegate next, CancellationToken cancellationToken);
    }
}
