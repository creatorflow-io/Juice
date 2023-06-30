using Juice.Domain;
using Juice.MultiTenant.Api.Identity;
using Juice.MultiTenant.Domain.Events;
using Microsoft.AspNetCore.Http;

namespace Juice.MultiTenant.Api.CommandHandlers.Tenants
{
    public class CreateTenantCommandHandler
        : IRequestHandler<CreateTenantCommand, IOperationResult>
    {
        private readonly TenantStoreDbContext _dbContext;
        private readonly ILogger _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IOwnerResolver _ownerResolver;
        public CreateTenantCommandHandler(TenantStoreDbContext dbContext
            , ILogger<CreateTenantCommandHandler> logger
            , IHttpContextAccessor httpContextAccessor
            , IOwnerResolver ownerResolver
            )
        {
            _dbContext = dbContext;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _ownerResolver = ownerResolver;
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

                tenant.AddDomainEvent(new TenantCreatedDomainEvent(tenant.Id, tenant.Identifier));
                tenant.UpdateProperties(request.Properties);

                var adminUser = tenant.GetProperty<string?>(() => default, "AdminUser");
                // try to set the owner info from current user
                if (string.IsNullOrEmpty(adminUser)
                    && (_httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false))
                {
                    var owner = await _ownerResolver.GetOwnerAsync(_httpContextAccessor.HttpContext.User);
                    if (!string.IsNullOrEmpty(owner))
                    {
                        tenant.SetOwner(owner);
                    }
                }
                else if (!string.IsNullOrEmpty(adminUser))
                {
                    var owner = await _ownerResolver.GetOwnerAsync(adminUser);
                    if (!string.IsNullOrEmpty(owner))
                    {
                        tenant.SetOwner(owner);
                    }
                }

                _dbContext.Add(tenant);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                // Exception may not raise here if TransactionBehavior was resgiterd in mediator pipeline
                _logger.LogError("Failed to create tenant {identifier}. {message}", request.Identifier, ex.Message);
                _logger.LogTrace(ex, "Failed to create tenant {identifier}.", request.Identifier);
                return OperationResult.Failed(ex, $"Failed to create tenant {request.Identifier}. {ex.Message}");
            }
        }
    }

    // Use for Idempotency in Command process
    public class CreateTenantIdentifiedCommandHandler
        : IdentifiedCommandHandler<CreateTenantCommand>
    {
        public CreateTenantIdentifiedCommandHandler(IMediator mediator, IRequestManager requestManager, ILogger<CreateTenantIdentifiedCommandHandler> logger)
            : base(mediator, requestManager, logger)
        {
        }

        protected override Task<IOperationResult?> CreateResultForDuplicateRequestAsync(IdentifiedCommand<CreateTenantCommand> mesage)
            => Task.FromResult((IOperationResult)OperationResult.Success);
        protected override (string IdProperty, string CommandId) ExtractInfo(CreateTenantCommand command)
            => (nameof(command.Identifier), command.Identifier);
    }
}
