using Juice.MultiTenant.Api.Commands.Tenants;
using Juice.MultiTenant.Domain.AggregatesModel.SettingsAggregate;

namespace Juice.MultiTenant.Api.CommandHandlers.Tenants
{
    public class DeleteSettingsCommandHandler : IRequestHandler<DeleteSettingsCommand, IOperationResult>
    {
        private readonly ITenantSettingsRepository _repository;
        public DeleteSettingsCommandHandler(ITenantSettingsRepository repository)
        {
            _repository = repository;
        }
        public async Task<IOperationResult> Handle(DeleteSettingsCommand request, CancellationToken cancellationToken)
        {
            try
            {
                await _repository.DeleteAsync(request.Section);
                return OperationResult.Success;// exception may be handle in mediatR behavior
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex, "Failed to delete settings. " + ex.Message);
            }
        }
    }
}
