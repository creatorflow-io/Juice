﻿namespace Juice.Workflows.Nodes.Gateways
{
    public class ParallelGateway : Gateway
    {
        private ILogger _logger;
        public ParallelGateway(ILogger<ParallelGateway> logger,
            IStringLocalizerFactory stringLocalizer) : base(stringLocalizer)
        {
            _logger = logger;
        }

        public override LocalizedString DisplayText => Localizer["Parallel Gateway"];

        public override async Task<NodeExecutionResult> StartAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flow, CancellationToken token)
        {
            _logger.LogInformation(node.Record.Name + " execute");
            if (flow == null)
            {
                return Fault("ParallelGateway required single incoming flow");
            }
            if (!workflowContext.AllFlowActiveTo(node))
            {
                return Noop("ParallelGateway must has completed for each of the incoming sequence flows");
            }
            if (workflowContext.IsNodeFinished(node.Record.Id))
            {
                return Noop("ParallelGateway is alreay finished");
            }
            return JoinnedOutcomes(workflowContext, node);

        }

        public override Task<NodeExecutionResult> ResumeAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token)
            => throw new NotImplementedException();
    }
}
