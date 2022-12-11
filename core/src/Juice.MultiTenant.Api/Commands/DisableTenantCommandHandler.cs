using Juice.MultiTenant.EF;
using Juice.MultiTenant.Events;
using Microsoft.EntityFrameworkCore;

namespace Juice.MultiTenant.Api.Commands
{
    public class DisableTenantCommandHandler
        : IRequestHandler<DisableTenantCommand, IOperationResult>
    {
        private readonly TenantStoreDbContext<Tenant> _dbContext;
        private readonly ILogger _logger;
        private readonly IMediator _mediator;

        public DisableTenantCommandHandler(TenantStoreDbContext<Tenant> dbContext
            , ILogger<DisableTenantCommandHandler> logger
            , IMediator mediator)
        {
            _dbContext = dbContext;
            _logger = logger;
            _mediator = mediator;
        }
        public async Task<IOperationResult> Handle(DisableTenantCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var tenant = await _dbContext.TenantInfo
                                .Where(ti => ti.Id == request.Id).FirstOrDefaultAsync();
                if (tenant == null)
                {
                    return OperationResult.Failed("Tenant not found");
                }
                tenant.Disable();
                await _dbContext.SaveChangesAsync();

                var evt = new TenantDeactivatedDomainEvent(tenant);
                await _mediator.Publish(evt);

                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to disable tenant {id}. {message}", request.Id, ex.Message);
                _logger.LogTrace(ex, "Failed to disable tenant {id}.", request.Id);
                return OperationResult.Failed(ex, $"Failed to disable tenant {request.Id}. {ex.Message}");
            }
        }
    }

    public class DisableTenantIdentifiedCommandHandler
        : IdentifiedCommandHandler<DisableTenantCommand, IOperationResult>
    {
        public DisableTenantIdentifiedCommandHandler(IMediator mediator, IRequestManager requestManager, ILogger<DisableTenantIdentifiedCommandHandler> logger)
          : base(mediator, requestManager, logger)
        {
        }

        protected override IOperationResult CreateResultForDuplicateRequest() => OperationResult.Success;

        protected override (string IdProperty, string CommandId) ExtractInfo(DisableTenantCommand command)
            => (nameof(command.Id), command.Id);
    }
}
