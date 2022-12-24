﻿namespace Juice.Workflows.Nodes
{
    public class StartEvent : Event
    {

        public StartEvent(IStringLocalizer<StartEvent> stringLocalizer) : base(stringLocalizer)
        {
        }

        public override LocalizedString DisplayText => Localizer["Start Event"];

        public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowContext workflowContext, NodeContext node)
            => new List<Outcome> { new Outcome(Localizer["Catched"]) };

        public override Task<NodeExecutionResult> ExecuteAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flowContext, CancellationToken token)
            => Task.FromResult(Outcomes("Catched"));

    }
}
