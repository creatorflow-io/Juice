using Grpc.Core;
using Juice.Workflows.Domain.AggregatesModel.EventAggregate;
using Juice.Workflows.Domain.Commands;
using Juice.Workflows.Grpc;
using MediatR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Juice.Workflows.Api.Grpc.Services
{
    internal class WorkflowService : Workflow.WorkflowBase
    {
        private readonly IMediator _mediator;
        private IEventRepository _eventRepository;
        private ILogger _logger;

        public WorkflowService(IMediator mediator, IEventRepository eventRepository,
            ILogger<WorkflowService> logger)
        {
            _mediator = mediator;
            _eventRepository = eventRepository;
            _logger = logger;
        }

        public override async Task<WorkflowOperationResult> Start(
            StartWorkflowMessage request, ServerCallContext context)
        {
            var parameters = JsonConvert.DeserializeObject<Dictionary<string, object?>>(request.SerializedParameters);
            var rs = await _mediator.Send(new StartWorkflowCommand(request.WorkflowId, request.CorrelationId, request.Name, parameters));
            if (rs != null)
            {
                return new WorkflowOperationResult
                {
                    Succeeded = rs.Succeeded,
                    Message = rs.Message,
                    WorkflowId = rs.Data?.Context?.WorkflowId
                };
            }
            else
            {
                return new WorkflowOperationResult
                {
                    Succeeded = false,
                    Message = "Invalid workflow operation result",
                };
            }
        }

        public override async Task<WorkflowOperationResult> Resume(ResumeWorkflowMessage request, ServerCallContext context)
        {
            var parameters = JsonConvert.DeserializeObject<Dictionary<string, object?>>(request.SerializedParameters);
            var rs = await _mediator.Send(new ResumeWorkflowCommand(request.WorkflowId, request.NodeId, parameters));
            if (rs != null)
            {
                return new WorkflowOperationResult
                {
                    Succeeded = rs.Succeeded && (rs.Data?.IsExecuted ?? false),
                    Message = rs.Data?.Message ?? rs.Message
                };
            }
            else
            {
                return new WorkflowOperationResult
                {
                    Succeeded = false,
                    Message = "Invalid workflow operation result",
                };
            }
        }

        public override async Task<WorkflowOperationResult> Catch(CatchMessage request, ServerCallContext context)
        {
            var parameters = JsonConvert.DeserializeObject<Dictionary<string, object?>>(request.SerializedParameters);

            if (string.IsNullOrEmpty(request.CallbackId)
                && (string.IsNullOrEmpty(request.CorrelationId) || string.IsNullOrEmpty(request.EventName))
                )
            {
                throw new ArgumentException("request must consits CallbackId or CorrelationId and EventName info");
            }

            if (!string.IsNullOrEmpty(request.CallbackId)
                && Guid.TryParse(request.CallbackId, out var callbackId))
            {
                var rs = await _mediator.Send(new DispatchWorkflowEventCommand(callbackId, request.IsCompleted, parameters));
                if (rs != null)
                {
                    return new WorkflowOperationResult
                    {
                        Succeeded = rs.Succeeded,
                        Message = rs.Message
                    };
                }
                else
                {
                    return new WorkflowOperationResult
                    {
                        Succeeded = false,
                        Message = "Invalid workflow operation result",
                    };
                }
            }
            else
            {
                var pendingEvents = await _eventRepository.FindAllAsync(e => !e.IsCompleted
                     // not required correlationId on start node 
                     && ((e.IsStartEvent && string.IsNullOrEmpty(e.CorrelationId)) || e.CorrelationId == request.CorrelationId)
                     && e.CatchingKey == request.EventName,
                     default);
                var hasError = false;
                var errors = new List<string>();
                if (pendingEvents.Any())
                {
                    var processedWorkflowIds = new HashSet<string>();

                    foreach (var pendingEvent in pendingEvents)
                    {
                        if (pendingEvent.IsStartEvent)
                        {
                            await _mediator.Send(new DispatchWorkflowEventCommand(pendingEvent.Id, request.IsCompleted, parameters));
                        }
                        else
                        {
                            if (processedWorkflowIds.Add(pendingEvent.WorkflowId))
                            {
                                var rs = await _mediator.Send(new DispatchWorkflowEventCommand(pendingEvent.Id, request.IsCompleted, parameters));
                                if (!rs.Succeeded)
                                {
                                    processedWorkflowIds.Remove(pendingEvent.WorkflowId);
                                    hasError = true;
                                    if (!string.IsNullOrEmpty(rs.Message))
                                    {
                                        errors.Add(rs.Message);
                                    }
                                }
                            }
                            else // duplicated event
                            {
                                if (request.IsCompleted) { pendingEvent.Complete(); }
                                else { pendingEvent.MarkCalled(); }
                                await _eventRepository.UpdateAsync(pendingEvent, default);
                            }
                        }
                    }
                }
                else if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("No pending event found with CatchingKey {CatchingKey} and CorrelationId {CorrelationId}", request.EventName, request.CorrelationId);
                }
                return new WorkflowOperationResult
                {
                    Succeeded = !hasError,
                    Message = string.Join(", ", errors)
                };
            }
        }
    }
}
