
using Juice.MultiTenant.Shared.Enums;

namespace Juice.MultiTenant.Api.CommandHandlers.Tenants
{
    /// <summary>
    /// Handle tenant status for the admin.
    /// </summary>
    public class AdminStatusCommandHandler
        : IRequestHandler<AdminStatusCommand, IOperationResult>
    {
        private readonly TenantStoreDbContext _dbContext;
        private readonly ILogger _logger;

        public AdminStatusCommandHandler(TenantStoreDbContext dbContext
            , ILogger<AdminStatusCommandHandler> logger
            )
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<IOperationResult> Handle(AdminStatusCommand request, CancellationToken cancellationToken)
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
                    case TenantStatus.PendingToActive:
                        tenant.RequestActivate();
                        break;
                    case TenantStatus.Active:
                        tenant.Activate();
                        break;
                    case TenantStatus.Inactive:
                        tenant.Deactivate();
                        break;
                    case TenantStatus.Suspended:
                        tenant.Suspend();
                        break;
                    default:
                        throw new InvalidOperationException($"Request status {request.Status} is not valid.");
                }

                await _dbContext.SaveChangesAsync();
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to change the activating status {id}. {message}", request.Id, ex.Message);
                _logger.LogTrace(ex, "Failed to change the activating status {id}.", request.Id);
                return OperationResult.Failed(ex, $"Failed to change the activating status {request.Id}. {ex.Message}");
            }
        }
    }


    public class ActivateProcessIdentifiedCommandHandler
        : IdentifiedCommandHandler<AdminStatusCommand>
    {
        public ActivateProcessIdentifiedCommandHandler(IMediator mediator,
            IRequestManager requestManager, ILogger<ActivateProcessIdentifiedCommandHandler> logger)
          : base(mediator, requestManager, logger)
        {
        }

        protected override Task<IOperationResult> CreateResultForDuplicateRequestAsync(IdentifiedCommand<AdminStatusCommand> mesage)
            => Task.FromResult((IOperationResult)OperationResult.Success);
        protected override (string IdProperty, string CommandId) ExtractInfo(AdminStatusCommand command)
            => (nameof(command.Id), command.Id);
    }
}
