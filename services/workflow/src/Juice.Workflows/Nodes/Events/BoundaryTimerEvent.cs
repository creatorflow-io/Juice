using Juice.Workflows.Domain.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Workflows.Nodes.Events
{
    public class BoundaryTimerEvent : BoundaryEvent
    {
        private ILogger _logger;
        private IServiceScopeFactory _scopeFactory;
        public BoundaryTimerEvent(ILogger<BoundaryTimerEvent> logger, IServiceScopeFactory scopeFactory,
            IStringLocalizerFactory stringLocalizer) : base(stringLocalizer)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public override LocalizedString DisplayText => Localizer["Timer Event"];

        public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowContext workflowContext, NodeContext node)
            => new List<Outcome> { new Outcome(Localizer["Throwed"]) };

        public override Task<bool> PreStartCheckAsync(WorkflowContext workflowContext, NodeContext node, NodeContext ancestor, CancellationToken token)
        {
            return Task.FromResult(true);
        }

        public override async Task<NodeExecutionResult> StartAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flow, CancellationToken token)
        {
            try
            {
                // Should register a timer
                using var scope = _scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var rs = await mediator.Send(new StartEventCommand<BoundaryTimerEvent>(workflowContext.WorkflowId, workflowContext.CorrelationId, node));
                if (rs.Succeeded)
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Registed a timer");
                    }
                    workflowContext.AddDomainEvent(new TimerEventStartDomainEvent(workflowContext.WorkflowId, node));
                    return Halt();
                }
                return Fault(rs.Message ?? "Failed to start a timer");
            }
            catch (Exception ex)
            {
                return Fault(ex.Message);
            }
        }

        public override Task<NodeExecutionResult> ResumeAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token)
        {
            _logger.LogInformation("Timed out");
            return Task.FromResult(Outcomes("Throwed"));
        }
    }
}
