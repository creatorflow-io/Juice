﻿namespace Juice.Workflows.Nodes.Events
{
    public class StartEvent : Event, ICatching
    {

        public StartEvent(IStringLocalizerFactory stringLocalizer) : base(stringLocalizer)
        {
        }

        public override LocalizedString DisplayText => Localizer["Start Event"];

        public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowContext workflowContext, NodeContext node)
            => new List<Outcome> { new Outcome(Localizer["Catched"]) };

        public override Task<NodeExecutionResult> StartAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flowContext, CancellationToken token)
        {
            workflowContext.AddDomainEvent(new ProcessStartedDomainEvent(node));
            return Task.FromResult(Outcomes("Catched"));
        }

    }
}
