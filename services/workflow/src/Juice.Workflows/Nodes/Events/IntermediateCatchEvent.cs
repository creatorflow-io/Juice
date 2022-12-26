namespace Juice.Workflows.Nodes.Events
{
    public abstract class IntermediateCatchEvent : Event, IIntermediate, ICatching
    {
        protected IntermediateCatchEvent(IStringLocalizerFactory stringLocalizer) : base(stringLocalizer)
        {
        }

        public override async Task<NodeExecutionResult> StartAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flow, CancellationToken token)
        {
            return Halt();
        }

        public override async Task<NodeExecutionResult> ResumeAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token)
        {
            var incomings = workflowContext.GetIncomings(node);
            var incoming = incomings.Single();
            var ancestor = workflowContext.GetNode(incoming.Record.SourceRef);
            if (ancestor == null)
            {
                return Fault("Ancestor node not found");
            }

            if (ancestor.Node is IEventBased)
            {
                if (ancestor.Node is IExclusive)
                {
                    var ancestorBranches = workflowContext.GetOutgoings(ancestor);
                    foreach (var branch in ancestorBranches)
                    {
                        if (branch.Record.DestinationRef != node.Record.Id)
                        {
                            if (workflowContext.IsFinished(branch.Record.DestinationRef))
                            {
                                return Noop("Another flow branch was happened");
                            }
                            else
                            {
                                // cancel other catch events
                                workflowContext.CancelBlockingEvent(branch.Record.DestinationRef);
                            }
                        }
                    }
                }
            }

            return Outcomes();
        }

        public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowContext workflowContext, NodeContext node)
            => new Outcome[] { new Outcome(Localizer["Catched"]) };
    }
}
