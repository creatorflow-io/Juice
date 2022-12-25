using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Juice.Extensions;

namespace Juice.Workflows.Tests.Nodes
{
    internal class OutcomeBranchUserTask : Activity
    {
        public override LocalizedString DisplayText => Localizer["Branch selector"];


        public OutcomeBranchUserTask(IServiceProvider serviceProvider, IStringLocalizer<UserTask> stringLocalizer)
            : base(serviceProvider, stringLocalizer)
        {
        }
        public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowContext workflowContext, NodeContext node)
            => new Outcome[] {
                    new Outcome(Localizer["branch1"]),
                    new Outcome(Localizer["branch3"])
            };
        public override Task<NodeExecutionResult> StartAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flow, CancellationToken token)
            => Task.FromResult(Outcomes(workflowContext.Input.GetOptionAsString("branch")));
    }
}
