using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Juice.Extensions;
using Microsoft.Extensions.Logging;

namespace Juice.Workflows.Tests.Nodes
{
    internal class OutcomeBranchUserTask : Activity
    {
        public override LocalizedString DisplayText => Localizer["Branch selector"];


        public OutcomeBranchUserTask(ILoggerFactory logger, IStringLocalizer<UserTask> stringLocalizer)
            : base(logger, stringLocalizer)
        {
        }
        public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowContext workflowContext, NodeContext node)
            => new Outcome[] {
                    new Outcome(Localizer["branch1"]),
                    new Outcome(Localizer["branch3"])
            };
        public override Task<NodeExecutionResult> ExecuteAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flow, CancellationToken token)
            => Task.FromResult(Outcomes(workflowContext.Input.GetOptionAsString("branch")));
    }
}
