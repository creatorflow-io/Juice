using Juice.MultiTenant.Shared.Enums;

namespace Juice.MultiTenant.Api.CommandHandlers.Tenants
{
    public class OperationStatusCommandHandler
        : IRequestHandler<OperationStatusCommand, IOperationResult>
    {
        private readonly TenantStoreDbContext _dbContext;
        private readonly ILogger _logger;

        public OperationStatusCommandHandler(TenantStoreDbContext dbContext
            , ILogger<OperationStatusCommandHandler> logger
            )
        {
            _dbContext = dbContext;
            _logger = logger;
        }
        public async Task<IOperationResult> Handle(OperationStatusCommand request, CancellationToken cancellationToken)
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
                    case TenantStatus.Active:
                        tenant.Reactivate();
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
                _logger.LogError("Failed to change the operation status {id}. {message}", request.Id, ex.Message);
                _logger.LogTrace(ex, "Failed to change the operation status {id}.", request.Id);
                return OperationResult.Failed(ex, $"Failed to change the operation status {request.Id}. {ex.Message}");
            }
        }
    }


    public class OperationStatusIdentifiedCommandHandler
        : IdentifiedCommandHandler<OperationStatusCommand>
    {
        public OperationStatusIdentifiedCommandHandler(IMediator mediator,
            IRequestManager requestManager, ILogger<OperationStatusIdentifiedCommandHandler> logger)
          : base(mediator, requestManager, logger)
        {
        }

        protected override Task<IOperationResult> CreateResultForDuplicateRequestAsync(IdentifiedCommand<OperationStatusCommand> mesage)
            => Task.FromResult((IOperationResult)OperationResult.Success);
        protected override (string IdProperty, string CommandId) ExtractInfo(OperationStatusCommand command)
            => (nameof(command.Id), command.Id);
    }
}
