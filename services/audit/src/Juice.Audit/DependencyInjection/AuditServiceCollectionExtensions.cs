using Juice.Audit;
using Juice.Audit.Services;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class AuditServiceCollectionExtensions
    {
        public static IServiceCollection AddAuditServices(this IServiceCollection services)
        {
            services.AddScoped<IAuditContextAccessor, DefaultAuditContextAccessor>();
            services.AddScoped<IAuditService, DefaultAuditService>();
            return services;
        }
    }
}
