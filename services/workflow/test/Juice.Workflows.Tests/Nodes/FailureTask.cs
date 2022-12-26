using System;
using System.Threading;
using System.Threading.Tasks;

namespace Juice.Workflows.Tests.Nodes
{
    internal class FailureTask : Activity
    {
        public FailureTask(IServiceProvider serviceProvider, IStringLocalizerFactory stringLocalizer) : base(serviceProvider, stringLocalizer)
        {
        }

        public override LocalizedString DisplayText => Localizer["Failure Task"];

        public override Task<NodeExecutionResult> ResumeAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token)
            => Task.FromResult(Fault("Exception throwed"));
    }
}
