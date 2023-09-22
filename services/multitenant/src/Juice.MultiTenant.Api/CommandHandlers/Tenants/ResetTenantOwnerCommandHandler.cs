using Juice.MultiTenant.Api.Identity;

namespace Juice.MultiTenant.Api.CommandHandlers.Tenants
{
    public class ResetTenantOwnerCommandHandler
        : IRequestHandler<ResetTenantOwnerCommand, IOperationResult>
    {
        private readonly TenantStoreDbContext _dbContext;
        private readonly ILogger _logger;
        private readonly IOwnerResolver _ownerResolver;

        public ResetTenantOwnerCommandHandler(TenantStoreDbContext dbContext
            , ILogger<ResetTenantOwnerCommandHandler> logger
            , IOwnerResolver ownerResolver
            )
        {
            _dbContext = dbContext;
            _logger = logger;
            _ownerResolver = ownerResolver;
        }
        public async Task<IOperationResult> Handle(ResetTenantOwnerCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var tenant = await _dbContext.TenantInfo
                                .Where(ti => ti.Id == request.Id).FirstOrDefaultAsync();
                if (tenant == null)
                {
                    return OperationResult.Failed("Tenant not found");
                }

                var owner = await _ownerResolver.GetOwnerAsync(request.OwnerUser);
                if (owner == null)
                {
                    return OperationResult.Failed("Owner not found");
                }

                tenant.SetOwner(owner);

                await _dbContext.SaveChangesAsync();
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to reset tenant {id}'s owner. {message}", request.Id, ex.Message);
                _logger.LogTrace(ex, "Failed to reset tenant {id}'s owner.", request.Id);
                return OperationResult.Failed(ex, $"Failed to reset tenant's owner {request.Id}. {ex.Message}");
            }
        }
    }


    public class ResetTenantOwnerIdentifiedCommandHandler
        : IdentifiedCommandHandler<ResetTenantOwnerCommand>
    {
        public ResetTenantOwnerIdentifiedCommandHandler(IMediator mediator,
            IRequestManager requestManager, ILogger<ResetTenantOwnerIdentifiedCommandHandler> logger)
          : base(mediator, requestManager, logger)
        {
        }

        protected override Task<IOperationResult> CreateResultForDuplicateRequestAsync(IdentifiedCommand<ResetTenantOwnerCommand> mesage)
            => Task.FromResult((IOperationResult)OperationResult.Success);
        protected override (string IdProperty, string CommandId) ExtractInfo(ResetTenantOwnerCommand command)
            => (nameof(command.Id), command.Id);
    }
}
