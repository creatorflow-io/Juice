namespace Juice.Workflows.Nodes.Activities
{
    public class DummyNode : Node
    {
        private readonly ILogger _logger;
        public DummyNode(IStringLocalizer<DummyNode> localizer, ILogger<DummyNode> logger)
            : base(localizer)
        {
            _logger = logger;
        }
        public override LocalizedString DisplayText => Localizer["Dummy Activity"];

        public override LocalizedString Category => Localizer["Exceptions"];

        public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowContext workflowContext, NodeContext node)
            => new List<Outcome>();

        public override Task<NodeExecutionResult> StartAsync(WorkflowContext workflowContext, NodeContext node,
            FlowContext? flowContext, CancellationToken token)
        {
            var message = "The flow node of type '{0}' was not registerd for '{1}'. Either enable the feature, or remove this activity from workflow definition with name '{2}'.";
            _logger.LogWarning(message, node.Record.Name,
                node.Record.Name,
                workflowContext.Name);
            return Task.FromResult(Noop(Localizer[message, node.Record.Name,
                node.Record.Name,
                workflowContext.Name]));
        }
        public override Task<NodeExecutionResult> ResumeAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token) => throw new NotImplementedException();
    }
}
