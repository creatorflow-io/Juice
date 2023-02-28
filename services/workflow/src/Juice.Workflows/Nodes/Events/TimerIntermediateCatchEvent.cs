using Juice.Workflows.Domain.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Workflows.Nodes.Events
{
    public class TimerIntermediateCatchEvent : IntermediateCatchEvent
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger _logger;
        public TimerIntermediateCatchEvent(IServiceScopeFactory scopeFactory,
            ILogger<TimerIntermediateCatchEvent> logger,
            IStringLocalizerFactory stringLocalizer)
            : base(stringLocalizer)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public override LocalizedString DisplayText => Localizer["Timer Intermediate Catch Event"];

        public override async Task<NodeExecutionResult> StartAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flow, CancellationToken token)
        {
            try
            {
                // Should register a timer
                using var scope = _scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var rs = await mediator.Send(new StartTimerCommand(workflowContext.WorkflowId, workflowContext.CorrelationId, node));
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
    }
}
