using Juice.Workflows.Domain.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Workflows.Nodes.Events
{
    public class MessageIntermediateCatchEvent : IntermediateCatchEvent
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger _logger;
        public MessageIntermediateCatchEvent(IServiceScopeFactory scopeFactory,
            ILogger<TimerIntermediateCatchEvent> logger,
            IStringLocalizerFactory stringLocalizer)
            : base(stringLocalizer)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public override LocalizedString DisplayText => Localizer["Message Intermediate Catch Event"];

        public override async Task<NodeExecutionResult> StartAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flow, CancellationToken token)
        {
            try
            {
                // Should register a timer
                using var scope = _scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var rs = await mediator.Send(new StartEventCommand<MessageIntermediateCatchEvent>(workflowContext.WorkflowId, workflowContext.CorrelationId, node));
                if (rs.Succeeded)
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Registed a message intermediate catch event");
                    }
                    return Halt();
                }
                return Fault(rs.Message ?? "Failed to start a message intermediate catch event");
            }
            catch (Exception ex)
            {
                return Fault(ex.Message);
            }
        }
    }
}
