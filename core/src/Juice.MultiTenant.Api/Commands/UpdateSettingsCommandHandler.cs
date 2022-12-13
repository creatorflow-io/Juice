using Juice.MultiTenant.Domain.AggregatesModel.SettingsAggregate;

namespace Juice.MultiTenant.Api.Commands
{
    public class UpdateSettingsCommandHandler
        : IRequestHandler<UpdateSettingsCommand, IOperationResult>
    {
        private readonly ITenantSettingsRepository _repository;
        public UpdateSettingsCommandHandler(ITenantSettingsRepository repository)
        {
            _repository = repository;
        }
        public async Task<IOperationResult> Handle(UpdateSettingsCommand request, CancellationToken cancellationToken)
        {
            try
            {
                await _repository.UpdateSectionAsync(request.Section, request.Options);
                return OperationResult.Success;// exception may be handle in mediatR behavior
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex, "Failed to update settings. " + ex.Message);
            }
        }
    }
}
