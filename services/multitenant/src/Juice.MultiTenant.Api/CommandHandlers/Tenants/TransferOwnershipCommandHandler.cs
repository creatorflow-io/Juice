using Juice.MultiTenant.Api.Identity;
using Microsoft.AspNetCore.Http;

namespace Juice.MultiTenant.Api.CommandHandlers.Tenants
{
    public class TransferOwnershipCommandHandler
        : IRequestHandler<TransferOwnershipCommand, IOperationResult>
    {
        private readonly TenantStoreDbContext _dbContext;
        private readonly ILogger _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IOwnerResolver _ownerResolver;

        public TransferOwnershipCommandHandler(TenantStoreDbContext dbContext
            , ILogger<TransferOwnershipCommandHandler> logger
            , IHttpContextAccessor httpContextAccessor
            , IOwnerResolver ownerResolver
            )
        {
            _dbContext = dbContext;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _ownerResolver = ownerResolver;
        }
        public async Task<IOperationResult> Handle(TransferOwnershipCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var tenant = await _dbContext.TenantInfo
                                .Where(ti => ti.Id == request.Id).FirstOrDefaultAsync();
                if (tenant == null)
                {
                    return OperationResult.Failed("Tenant not found");
                }

                var principal = _httpContextAccessor.HttpContext?.User;

                var currentOwner = principal != null ?
                    await _ownerResolver.GetOwnerAsync(principal)
                    : default(string);

                var owner = await _ownerResolver.GetOwnerAsync(request.OwnerUser);

                if (owner == null)
                {
                    return OperationResult.Failed("Owner not found");
                }

                tenant.TransferOwner(currentOwner, owner);

                await _dbContext.SaveChangesAsync();
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to transfer tenant {id}'s owner. {message}", request.Id, ex.Message);
                _logger.LogTrace(ex, "Failed to transfer tenant {id}'s owner.", request.Id);
                return OperationResult.Failed(ex, $"Failed to transfer tenant's owner {request.Id}. {ex.Message}");
            }
        }
    }


    public class TransferOwnershipIdentifiedCommandHandler
        : IdentifiedCommandHandler<TransferOwnershipCommand>
    {
        public TransferOwnershipIdentifiedCommandHandler(IMediator mediator,
            IRequestManager requestManager, ILogger<TransferOwnershipIdentifiedCommandHandler> logger)
          : base(mediator, requestManager, logger)
        {
        }

        protected override Task<IOperationResult> CreateResultForDuplicateRequestAsync(IdentifiedCommand<TransferOwnershipCommand> mesage)
            => Task.FromResult((IOperationResult)OperationResult.Success);
        protected override (string IdProperty, string CommandId) ExtractInfo(TransferOwnershipCommand command)
            => (nameof(command.Id), command.Id);
    }
}
