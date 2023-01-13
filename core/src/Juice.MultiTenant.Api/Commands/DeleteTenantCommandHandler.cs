using Juice.MultiTenant.Domain.Events;
using Juice.MultiTenant.EF;
using Microsoft.EntityFrameworkCore;

namespace Juice.MultiTenant.Api.Commands
{
    public class DeleteTenantCommandHandler
        : IRequestHandler<DeleteTenantCommand, IOperationResult>
    {
        private readonly TenantStoreDbContext<Tenant> _dbContext;
        private readonly ILogger _logger;
        private readonly IMediator _mediator;

        public DeleteTenantCommandHandler(TenantStoreDbContext<Tenant> dbContext
            , ILogger<DeleteTenantCommandHandler> logger
            , IMediator mediator)
        {
            _dbContext = dbContext;
            _logger = logger;
            _mediator = mediator;
        }
        public async Task<IOperationResult> Handle(DeleteTenantCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var tenant = await _dbContext.TenantInfo
                                .Where(ti => ti.Id == request.Id).FirstOrDefaultAsync();
                if (tenant == null)
                {
                    return OperationResult.Result(request.Id, "Tenant not found");
                }
                _dbContext.Remove(tenant);

                await _dbContext.SaveChangesAsync(cancellationToken);

                var evt = new TenantDeactivatedDomainEvent(tenant);
                await _mediator.Publish(evt);

                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to remove tenant {id}. {message}", request.Id, ex.Message);
                _logger.LogTrace(ex, "Failed to remove tenant {id}.", request.Id);
                return OperationResult.Failed(ex, $"Failed to remove tenant {request.Id}. {ex.Message}");
            }
        }
    }

    // Use for Idempotency in Command process
    public class DeleteTenantIdentifiedCommandHandler
        : IdentifiedCommandHandler<DeleteTenantCommand, IOperationResult>
    {
        public DeleteTenantIdentifiedCommandHandler(IMediator mediator, IRequestManager requestManager, ILogger<DeleteTenantIdentifiedCommandHandler> logger)
            : base(mediator, requestManager, logger)
        {
        }

        protected override Task<IOperationResult> CreateResultForDuplicateRequestAsync(IdentifiedCommand<DeleteTenantCommand, IOperationResult> mesage)
            => Task.FromResult((IOperationResult)OperationResult.Success);

        protected override (string IdProperty, string CommandId) ExtractInfo(DeleteTenantCommand command)
            => (nameof(command.Id), command.Id);
    }
}
