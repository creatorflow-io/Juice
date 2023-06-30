using Juice.MultiTenant.Api.Commands.Tenants;
using Juice.MultiTenant.EF;
using Microsoft.EntityFrameworkCore;

namespace Juice.MultiTenant.Api.CommandHandlers.Tenants
{
    public class UpdateTenantPropertiesCommandHandler
        : IRequestHandler<UpdateTenantPropertiesCommand, IOperationResult>
    {
        private readonly TenantStoreDbContext _dbContext;
        private readonly ILogger _logger;

        public UpdateTenantPropertiesCommandHandler(TenantStoreDbContext dbContext
            , ILogger<UpdateTenantPropertiesCommandHandler> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }
        public async Task<IOperationResult> Handle(UpdateTenantPropertiesCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var tenant = await _dbContext.TenantInfo
                                .Where(ti => ti.Id == request.Id).FirstOrDefaultAsync();
                if (tenant == null)
                {
                    return OperationResult.Failed("Tenant not found");
                }
                tenant.UpdateProperties(request.Properties);
                await _dbContext.SaveChangesAsync();
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to update tenant {id}. {message}", request.Id, ex.Message);
                _logger.LogTrace(ex, "Failed to update tenant {id}.", request.Id);
                return OperationResult.Failed(ex, $"Failed to update tenant {request.Id}. {ex.Message}");
            }
        }
    }

    public class UpdateTenantPropertiesIdentifiedCommandHandler
        : IdentifiedCommandHandler<UpdateTenantPropertiesCommand>
    {
        public UpdateTenantPropertiesIdentifiedCommandHandler(IMediator mediator, IRequestManager requestManager, ILogger<UpdateTenantPropertiesIdentifiedCommandHandler> logger)
            : base(mediator, requestManager, logger)
        {
        }

        protected override Task<IOperationResult> CreateResultForDuplicateRequestAsync(IdentifiedCommand<UpdateTenantPropertiesCommand> mesage)
            => Task.FromResult((IOperationResult)OperationResult.Success);

        protected override (string IdProperty, string CommandId) ExtractInfo(UpdateTenantPropertiesCommand command) => (nameof(command.Id), command.Id);
    }
}
