
using Juice.MultiTenant.Shared.Enums;

namespace Juice.MultiTenant.Api.CommandHandlers.Tenants
{
    public class InitializationProcessCommandHandler
        : IRequestHandler<InitializationProcessCommand, IOperationResult>
    {
        private readonly TenantStoreDbContext _dbContext;
        private readonly ILogger _logger;

        public InitializationProcessCommandHandler(TenantStoreDbContext dbContext
            , ILogger<InitializationProcessCommandHandler> logger
            )
        {
            _dbContext = dbContext;
            _logger = logger;
        }
        public async Task<IOperationResult> Handle(InitializationProcessCommand request, CancellationToken cancellationToken)
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
                    case TenantStatus.Initializing:
                        tenant.Initializing();
                        break;
                    case TenantStatus.Initialized:
                        tenant.Initialized();
                        break;
                    default:
                        return OperationResult.Failed($"Invalid state {request.Status}");
                }

                await _dbContext.SaveChangesAsync();
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to change initialization state {id}. {message}", request.Id, ex.Message);
                _logger.LogTrace(ex, "Failed to change initialization state {id}.", request.Id);
                return OperationResult.Failed(ex, $"Failed to change initialization state {request.Id}. {ex.Message}");
            }
        }
    }


    public class InitializationProcessIdentifiedCommandHandler
        : IdentifiedCommandHandler<InitializationProcessCommand>
    {
        public InitializationProcessIdentifiedCommandHandler(IMediator mediator,
            IRequestManager requestManager, ILogger<InitializationProcessIdentifiedCommandHandler> logger)
          : base(mediator, requestManager, logger)
        {
        }

        protected override Task<IOperationResult> CreateResultForDuplicateRequestAsync(IdentifiedCommand<InitializationProcessCommand> mesage)
            => Task.FromResult((IOperationResult)OperationResult.Success);
        protected override (string IdProperty, string CommandId) ExtractInfo(InitializationProcessCommand command)
            => (nameof(command.Id), command.Id);
    }
}
