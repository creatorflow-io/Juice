namespace Juice.Workflows.Nodes.Events
{
    public abstract class BoundaryEvent : Event, IBoundary
    {
        protected bool _cancelActivity = true;
        public BoundaryEvent(IStringLocalizerFactory stringLocalizer) : base(stringLocalizer)
        {
        }

        public void NonInterupt()
        {
            _cancelActivity = false;
        }

        public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowContext workflowContext, NodeContext node)
            => new List<Outcome> { new Outcome(Localizer["Catched"]) };
        public abstract Task<bool> PreStartCheckAsync(WorkflowContext workflowContext, NodeContext node, NodeContext ancestor, CancellationToken token);
    }
}
