
using Juice.MultiTenant.Shared.Enums;

namespace Juice.MultiTenant.Api.CommandHandlers.Tenants
{
    public class ApprovalProcessCommandHandler
        : IRequestHandler<ApprovalProcessCommand, IOperationResult>
    {
        private readonly TenantStoreDbContext _dbContext;
        private readonly ILogger _logger;

        public ApprovalProcessCommandHandler(TenantStoreDbContext dbContext
            , ILogger<AdminStatusCommandHandler> logger
            )
        {
            _dbContext = dbContext;
            _logger = logger;
        }
        public async Task<IOperationResult> Handle(ApprovalProcessCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var tenant = await _dbContext.TenantInfo
                                .Where(ti => ti.Id == request.Id).FirstOrDefaultAsync();
                if (tenant == null)
                {
                    return OperationResult.Failed("Tenant not found");
                }

                switch (request.Status)
                {
                    case TenantStatus.Approved:
                        tenant.Approved();
                        break;
                    case TenantStatus.Rejected:
                        tenant.Rejected();
                        break;
                    case TenantStatus.PendingApproval:
                        tenant.RequestApproval();
                        break;
                    default:
                        return OperationResult.Failed($"Invalid status {request.Status}");
                }

                await _dbContext.SaveChangesAsync();
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to change the approval status {id}. {message}", request.Id, ex.Message);
                _logger.LogTrace(ex, "Failed to change the approval status {id}.", request.Id);
                return OperationResult.Failed(ex, $"Failed to change the approval status {request.Id}. {ex.Message}");
            }
        }
    }


    public class ApprovalProcessIdentifiedCommandHandler
        : IdentifiedCommandHandler<ApprovalProcessCommand>
    {
        public ApprovalProcessIdentifiedCommandHandler(IMediator mediator,
            IRequestManager requestManager, ILogger<ApprovalProcessIdentifiedCommandHandler> logger)
          : base(mediator, requestManager, logger)
        {
        }

        protected override Task<IOperationResult> CreateResultForDuplicateRequestAsync(IdentifiedCommand<ApprovalProcessCommand> mesage)
            => Task.FromResult((IOperationResult)OperationResult.Success);
        protected override (string IdProperty, string CommandId) ExtractInfo(ApprovalProcessCommand command)
            => (nameof(command.Id), command.Id);
    }
}
