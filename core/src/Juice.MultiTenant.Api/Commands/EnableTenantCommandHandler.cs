using Juice.MultiTenant.EF;
using Juice.MultiTenant.Events;
using Microsoft.EntityFrameworkCore;

namespace Juice.MultiTenant.Api.Commands
{
    internal class EnableTenantCommandHandler
        : IRequestHandler<EnableTenantCommand, IOperationResult>
    {
        private readonly TenantStoreDbContext<Tenant> _dbContext;
        private readonly ILogger _logger;
        private readonly IMediator _mediator;

        public EnableTenantCommandHandler(TenantStoreDbContext<Tenant> dbContext
            , ILogger<EnableTenantCommandHandler> logger
            , IMediator mediator)
        {
            _dbContext = dbContext;
            _logger = logger;
            _mediator = mediator;
        }
        public async Task<IOperationResult> Handle(EnableTenantCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var tenant = await _dbContext.TenantInfo
                                .Where(ti => ti.Id == request.Id).FirstOrDefaultAsync();
                if (tenant == null)
                {
                    return OperationResult.Failed("Tenant not found");
                }
                tenant.Enable();

                var evt = new TenantActivatedDomainEvent(tenant);
                await _mediator.Publish(evt);

                await _dbContext.SaveChangesAsync();
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


    public class EnableTenantIdentifiedCommandHandler
        : IdentifiedCommandHandler<EnableTenantCommand, IOperationResult>
    {
        public EnableTenantIdentifiedCommandHandler(IMediator mediator,
            IRequestManager requestManager, ILogger<EnableTenantIdentifiedCommandHandler> logger)
          : base(mediator, requestManager, logger)
        {
        }

        protected override IOperationResult CreateResultForDuplicateRequest() => OperationResult.Success;

        protected override (string IdProperty, string CommandId) ExtractInfo(EnableTenantCommand command)
            => (nameof(command.Id), command.Id);
    }
}
