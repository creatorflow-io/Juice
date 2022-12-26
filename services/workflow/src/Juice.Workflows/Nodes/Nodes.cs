using Microsoft.Extensions.DependencyInjection;

namespace Juice.Workflows.Nodes
{
    public abstract class Activity : Node, IActivity
    {
        protected ILogger _logger;
        protected IServiceProvider _serviceProvider;
        protected Activity(IServiceProvider serviceProvider, IStringLocalizerFactory stringLocalizer) : base(stringLocalizer)
        {
            _serviceProvider = serviceProvider;
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            _logger = loggerFactory.CreateLogger(GetType());
        }

        public override LocalizedString Category => Localizer["Activities"];

        public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowContext workflowContext, NodeContext node)
            => new List<Outcome> { new Outcome(Localizer["Done"]) };

        public override async Task<NodeExecutionResult> StartAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flow, CancellationToken token)
        {
            if (flow == null)
            {
                return Fault("Activity required an income flow");
            }
            _logger.LogInformation(node.Record.Name + " execute");
            return Halt();
        }

        public override Task<NodeExecutionResult> ResumeAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token)
        {
            _logger.LogInformation(node.Record.Name + " outcome done");
            return Task.FromResult(Outcomes("Done"));
        }

    }

    public abstract class Event : Node, IEvent
    {
        protected Event(IStringLocalizerFactory stringLocalizer) : base(stringLocalizer)
        {
        }

        public override LocalizedString Category => Localizer["Events"];

        public override Task<NodeExecutionResult> ResumeAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token)
            => throw new NotImplementedException();

    }

    public abstract class Gateway : Node, IGateway
    {
        protected Gateway(IStringLocalizerFactory stringLocalizer) : base(stringLocalizer)
        {
        }

        public override LocalizedString Category => Localizer["Gateways"];

        public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowContext workflowContext, NodeContext node)
        {
            var outcomes = new List<Outcome>();
            var incomings = workflowContext.GetIncomings(node);
            foreach (var incoming in incomings)
            {
                var ancestor = workflowContext.GetNode(incoming.Record.SourceRef);
                if (ancestor == null)
                {
                    throw new InvalidOperationException("Ancestor node not found");
                }
                outcomes.AddRange(ancestor.Node.GetPossibleOutcomes(workflowContext, ancestor));
            }
            return outcomes;
        }

        protected NodeExecutionResult JoinnedOutcomes(WorkflowContext workflowContext, NodeContext node)
        {
            var outcomes = new List<string>();

            var incomings = workflowContext.GetIncomings(node);

            foreach (var incoming in incomings)
            {
                var ancestorOutcomes = workflowContext.GetOutcomes(incoming.Record.SourceRef);

                outcomes.AddRange(ancestorOutcomes);
            }

            return Outcomes(outcomes.ToArray());
        }

        protected NodeExecutionResult SourceOutcomes(WorkflowContext workflowContext, FlowContext incoming)
        {
            var outcomes = workflowContext.GetOutcomes(incoming.Record.SourceRef);
            return Outcomes(outcomes.ToArray());
        }

        public virtual Task PostExecuteCheckAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token)
            => Task.CompletedTask;

    }

}
