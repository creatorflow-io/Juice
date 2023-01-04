namespace Juice.Workflows.Bpmn.Models
{
    internal static class BPMNExtensions
    {
        public static bool Has<T>(this tEvent @event)
            where T : tEventDefinition
            => (@event is tCatchEvent catchEvent && (catchEvent.eventDefinition?.Any(e => e is T) ?? false))
            || (@event is tThrowEvent throwEvent && (throwEvent.eventDefinition?.Any(e => e is T) ?? false));
        public static bool IsTimer(this tEvent @event)
            => @event.Has<tTimerEventDefinition>();
        public static bool IsTerminate(this tEvent @event)
            => @event.Has<tTerminateEventDefinition>();
        public static bool IsCancel(this tEvent @event)
            => @event.Has<tCancelEventDefinition>();
        public static bool IsError(this tEvent @event)
            => @event.Has<tErrorEventDefinition>();
        public static bool IsMessage(this tEvent @event)
            => @event.Has<tMessageEventDefinition>();
        public static bool IsConditional(this tEvent @event)
            => @event.Has<tConditionalEventDefinition>();
        public static bool IsSignal(this tEvent @event)
            => @event.Has<tSignalEventDefinition>();
    }
}
