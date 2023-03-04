using Juice.Extensions;
using Juice.Workflows.Domain.AggregatesModel.EventAggregate;
using Juice.Workflows.Domain.Commands;
using MediatR;

namespace Juice.Workflows.Api.Domain.CommandHandlers
{
    public abstract class StartCatchCommandHandlerBase<TRequest> : IRequestHandler<TRequest, IOperationResult>
        where TRequest : INodeCommand, IRequest<IOperationResult>
    {
        private IEventRepository _eventRepository;
        public StartCatchCommandHandlerBase(IEventRepository eventRepository)
        {
            _eventRepository = eventRepository;
        }

        public virtual async Task<IOperationResult> Handle(TRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var catchKey = GetCatchEventKey(request);
                if (string.IsNullOrEmpty(catchKey))
                {
                    return OperationResult.Failed("CatchEvent must be configured on node");
                }
                var catchEvent = new EventRecord(request.WorkflowId, request.Node.Record.Id, false, request.CorrelationId, catchKey, request.Node.Record.Name);
                var catchEventRs = await _eventRepository.CreateUniqueByWorkflowAsync(catchEvent, cancellationToken);
                if (!catchEventRs.Succeeded)
                {
                    return catchEventRs;
                }

                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }

        protected virtual string? GetCatchEventKey(TRequest request)
        {
            var eventName = request.Node.Properties.GetOption<string?>("CatchEvent");

            return !string.IsNullOrEmpty(eventName) ? $"wfcatch.{eventName}" : default;
        }
    }
}
