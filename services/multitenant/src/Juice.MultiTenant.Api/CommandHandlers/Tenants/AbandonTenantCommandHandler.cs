namespace Juice.MultiTenant.Api.CommandHandlers.Tenants
{
    public class AbandonTenantCommandHandler
        : IRequestHandler<AbandonTenantCommand, IOperationResult>
    {
        private readonly TenantStoreDbContext _dbContext;
        private readonly ILogger _logger;

        public AbandonTenantCommandHandler(TenantStoreDbContext dbContext
            , ILogger<AbandonTenantCommandHandler> logger
            )
        {
            _dbContext = dbContext;
            _logger = logger;
        }
        public async Task<IOperationResult> Handle(AbandonTenantCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var tenant = await _dbContext.TenantInfo
                                .Where(ti => ti.Id == request.Id).FirstOrDefaultAsync();
                if (tenant == null)
                {
                    return OperationResult.Failed("Tenant not found");
                }
                tenant.Abandon();
                await _dbContext.SaveChangesAsync();

                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to abandon tenant {id}. {message}", request.Id, ex.Message);
                _logger.LogTrace(ex, "Failed to abandon tenant {id}.", request.Id);
                return OperationResult.Failed(ex, $"Failed to abandon tenant {request.Id}. {ex.Message}");
            }
        }
    }

    public class AbandonTenantIdentifiedCommandHandler
        : IdentifiedCommandHandler<AbandonTenantCommand, IOperationResult>
    {
        public AbandonTenantIdentifiedCommandHandler(IMediator mediator, IRequestManager requestManager, ILogger<AbandonTenantIdentifiedCommandHandler> logger)
          : base(mediator, requestManager, logger)
        {
        }

        protected override Task<IOperationResult> CreateResultForDuplicateRequestAsync(IdentifiedCommand<AbandonTenantCommand, IOperationResult> mesage)
            => Task.FromResult((IOperationResult)OperationResult.Success);
        protected override (string IdProperty, string CommandId) ExtractInfo(AbandonTenantCommand command)
            => (nameof(command.Id), command.Id);
    }
}
