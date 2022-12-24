namespace Juice.Workflows.Models
{
    public abstract class Activity : Node, IActivity
    {
        protected ILogger _logger;
        protected Activity(ILoggerFactory loggerFactory, IStringLocalizer stringLocalizer) : base(stringLocalizer)
        {
            _logger = loggerFactory.CreateLogger(GetType());
        }

        public override LocalizedString Category => Localizer["Activities"];

        public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowContext workflowContext, NodeContext node)
            => new List<Outcome> { new Outcome(Localizer["Done"]) };

        public override Task<NodeExecutionResult> ExecuteAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flow, CancellationToken token)
        {
            if (flow == null)
            {
                return Task.FromResult(Fault("Activity required an income flow"));
            }
            _logger.LogInformation(node.Record.Name + " execute");
            return Task.FromResult(Halt());
        }
        public override Task<NodeExecutionResult> ResumeAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token)
        {
            _logger.LogInformation(node.Record.Name + " outcome done");
            return Task.FromResult(Outcomes("Done"));
        }
    }

    public abstract class Event : Node, IEvent
    {
        protected Event(IStringLocalizer stringLocalizer) : base(stringLocalizer)
        {
        }

        public override LocalizedString Category => Localizer["Events"];

        public override Task<NodeExecutionResult> ResumeAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token)
            => throw new NotImplementedException();

    }

    public abstract class Gateway : Node, IGateway
    {
        protected Gateway(IStringLocalizer stringLocalizer) : base(stringLocalizer)
        {
        }

        public override LocalizedString Category => Localizer["Gateways"];

        public virtual Task PostCheckAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token)
            => Task.CompletedTask;


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
    }

}
