using Finbuckle.MultiTenant;
using Grpc.Core;
using Juice.MultiTenant.Settings.Grpc;
using Juice.MultiTenant.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.MultiTenant.Api.Grpc.Services
{
    public class TenantSettingsStoreService
        : TenantSettingsStore.TenantSettingsStoreBase
    {
        private readonly ITenantInfo? _tenantInfo;
        private readonly ILogger _logger;

        public TenantSettingsStoreService(
            ILogger<TenantSettingsStoreService> logger,
            ITenantInfo? tenantInfo = null)
        {
            _tenantInfo = tenantInfo;
            _logger = logger;
        }

        public override async Task<TenantSettingsResult> GetAll(TenantSettingQuery request, ServerCallContext context)
        {
            _logger.LogInformation("Tenant Identifier: " + _tenantInfo?.Identifier ?? "Unk");

            if (string.IsNullOrEmpty(_tenantInfo?.Identifier))
            {
                return new TenantSettingsResult
                {
                    Succeeded = false,
                    Message = "Tenant info is missing or could not be resolved."
                };
            }

            var repository = context.GetHttpContext().RequestServices.GetService<ITenantSettingsRepository>();
            if (repository == null)
            {
                _logger.LogError("ITenantSettingsRepository was not registerd.");
                return new TenantSettingsResult
                {
                    Succeeded = false,
                    Message = "Required service was not registerd."
                };
            }

            var result = new TenantSettingsResult
            {
                Succeeded = true
            };
            var data = (await repository!.GetAllAsync(context.CancellationToken))
                .ToDictionary<TenantSettings, string, string?>(c => c.Key, c => c.Value);

            result.Settings.Add(data);

            return result;

        }

        [Authorize(Policies.TenantSettingsPolicy)]
        public override async Task<UpdateSectionResult> UpdateSection(UpdateSectionParams request, ServerCallContext context)
        {
            var mediator = context.GetHttpContext().RequestServices.GetService<IMediator>();
            if (mediator == null)
            {
                return new UpdateSectionResult
                {
                    Succeeded = false,
                    Message = "Required service was not registerd."
                };
            }

            if (string.IsNullOrEmpty(request.Section))
            {
                return new UpdateSectionResult
                {
                    Succeeded = false,
                    Message = "Section is missing."
                };
            }

            var rs = await mediator.Send(new UpdateSettingsCommand(request.Section,
                request.Settings.ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value)));

            return new UpdateSectionResult
            {
                Succeeded = rs.Succeeded,
                Message = rs.Message
            };
        }


        [Authorize(Policies.TenantSettingsPolicy)]
        public override async Task<UpdateSectionResult> DeleteSection(UpdateSectionParams request, ServerCallContext context)
        {
            var mediator = context.GetHttpContext().RequestServices.GetService<IMediator>();
            if (mediator == null)
            {
                return new UpdateSectionResult
                {
                    Succeeded = false,
                    Message = "Required service was not registerd."
                };
            }

            if (string.IsNullOrEmpty(request.Section))
            {
                return new UpdateSectionResult
                {
                    Succeeded = false,
                    Message = "Section is missing."
                };
            }

            var rs = await mediator.Send(new DeleteSettingsCommand(request.Section));

            return new UpdateSectionResult
            {
                Succeeded = rs.Succeeded,
                Message = rs.Message
            };
        }
    }
}
