using Juice.MultiTenant.EF;

namespace Juice.MultiTenant.Api.Commands
{
    public class CreateTenantCommandHandler
        : IRequestHandler<CreateTenantCommand, IOperationResult>
    {
        private readonly TenantStoreDbContext<Tenant> _dbContext;
        private readonly ILogger _logger;

        public CreateTenantCommandHandler(TenantStoreDbContext<Tenant> dbContext
            , ILogger<CreateTenantCommandHandler> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }
        public async Task<IOperationResult> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.Identifier))
                {
                    return OperationResult.Failed("Identifier is required");
                }
                var tenant = new Tenant
                {
                    Id = request.Id ?? request.Identifier,
                    ConnectionString = request.ConnectionString,
                    Identifier = request.Identifier,
                    Name = request.Name
                };
                _dbContext.Add(tenant);
                await _dbContext.SaveChangesAsync(cancellationToken);
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create tenant {identifier}. {message}", request.Identifier, ex.Message);
                _logger.LogTrace(ex, "Failed to create tenant {identifier}.", request.Identifier);
                return OperationResult.Failed(ex, $"Failed to create tenant {request.Identifier}. {ex.Message}");
            }
        }
    }

    // Use for Idempotency in Command process
    public class CreateTenantIdentifiedCommandHandler
        : IdentifiedCommandHandler<CreateTenantCommand, IOperationResult>
    {
        public CreateTenantIdentifiedCommandHandler(IMediator mediator, IRequestManager requestManager, ILogger<CreateTenantIdentifiedCommandHandler> logger)
            : base(mediator, requestManager, logger)
        {
        }

        protected override IOperationResult CreateResultForDuplicateRequest() => OperationResult.Success;

        protected override (string IdProperty, string CommandId) ExtractInfo(CreateTenantCommand command)
            => (nameof(command.Identifier), command.Identifier);
    }
}
