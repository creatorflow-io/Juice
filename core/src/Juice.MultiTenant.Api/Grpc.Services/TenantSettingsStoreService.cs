using Finbuckle.MultiTenant;
using Grpc.Core;
using Juice.MultiTenant.Api.Commands;
using Juice.MultiTenant.Domain.AggregatesModel.SettingsAggregate;
using Juice.MultiTenant.EF;
using Juice.MultiTenant.Settings.Grpc;
using Microsoft.EntityFrameworkCore;
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

            var dbContext = context.GetHttpContext().RequestServices.GetService<TenantSettingsDbContext>();
            if (dbContext == null)
            {
                _logger.LogError("TenantSettingsDbContext was not registerd.");
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
            var data = await dbContext.Settings.ToDictionaryAsync<TenantSettings, string, string?>(c => c.Key, c => c.Value);

            result.Settings.Add(data);

            return result;

        }

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
