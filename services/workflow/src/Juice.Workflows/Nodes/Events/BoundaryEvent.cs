namespace Juice.Workflows.Nodes.Events
{
    public abstract class BoundaryEvent : Event, IBoundary
    {
        public BoundaryEvent(IStringLocalizer stringLocalizer) : base(stringLocalizer)
        {
        }

        public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowContext workflowContext, NodeContext node)
            => new List<Outcome> { new Outcome(Localizer["Throwed"]) };
        public abstract Task<bool> PreStartCheckAsync(WorkflowContext workflowContext, NodeContext node, NodeContext ancestor, CancellationToken token);
    }
}
