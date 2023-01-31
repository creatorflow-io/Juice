namespace Juice.Workflows.Nodes.Events
{
    public class SignalStartEvent : StartEvent
    {
        public SignalStartEvent(IStringLocalizerFactory stringLocalizer) : base(stringLocalizer)
        {
        }
    }
}
