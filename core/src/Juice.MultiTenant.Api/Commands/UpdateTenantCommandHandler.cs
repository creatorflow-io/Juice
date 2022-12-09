using Juice.MediatR;
using Juice.MultiTenant.EF;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Juice.MultiTenant.Api.Commands
{
    public class UpdateTenantCommandHandler
        : IRequestHandler<UpdateTenantCommand, IOperationResult>
    {
        private readonly TenantStoreDbContext<Tenant> _dbContext;
        private readonly ILogger _logger;

        public UpdateTenantCommandHandler(TenantStoreDbContext<Tenant> dbContext
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
                tenant.Name = request.Name;
                tenant.Identifier = request.Identifier;
                tenant.ConnectionString = request.ConnectionString;
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
        : IdentifiedCommandHandler<UpdateTenantCommand, IOperationResult>
    {
        public UpdateTenantIdentifiedCommandHandler(IMediator mediator, IRequestManager requestManager, ILogger<UpdateTenantIdentifiedCommandHandler> logger)
            : base(mediator, requestManager, logger)
        {
        }

        protected override (string IdProperty, string CommandId) ExtractInfo(UpdateTenantCommand command) => (nameof(command.Id), command.Id);
    }
}
