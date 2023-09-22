using Juice.MultiTenant.Api.Commands.Tenants;
using Juice.MultiTenant.EF;
using Microsoft.EntityFrameworkCore;

namespace Juice.MultiTenant.Api.CommandHandlers.Tenants
{
    public class UpdateTenantCommandHandler
        : IRequestHandler<UpdateTenantCommand, IOperationResult>
    {
        private readonly TenantStoreDbContext _dbContext;
        private readonly ILogger _logger;

        public UpdateTenantCommandHandler(TenantStoreDbContext dbContext
            , ILogger<UpdateTenantCommandHandler> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }
        public async Task<IOperationResult> Handle(UpdateTenantCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var tenant = await _dbContext.TenantInfo
                                .Where(ti => ti.Id == request.Id).FirstOrDefaultAsync();
                if (tenant == null)
                {
                    return OperationResult.Failed("Tenant not found");
                }
                tenant.Update(request.Name, request.Identifier, request.ConnectionString);
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

    public class UpdateTenantIdentifiedCommandHandler
        : IdentifiedCommandHandler<UpdateTenantCommand>
    {
        public UpdateTenantIdentifiedCommandHandler(IMediator mediator, IRequestManager requestManager, ILogger<UpdateTenantIdentifiedCommandHandler> logger)
            : base(mediator, requestManager, logger)
        {
        }

        protected override Task<IOperationResult> CreateResultForDuplicateRequestAsync(IdentifiedCommand<UpdateTenantCommand> mesage)
            => Task.FromResult((IOperationResult)OperationResult.Success);

        protected override (string IdProperty, string CommandId) ExtractInfo(UpdateTenantCommand command) => (nameof(command.Id), command.Id);
    }
}
