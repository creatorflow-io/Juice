namespace Juice.Workflows.Nodes.Events
{
    public class TimerStartEvent : StartEvent
    {
        public TimerStartEvent(IStringLocalizerFactory stringLocalizer) : base(stringLocalizer)
        {
        }
    }
}
