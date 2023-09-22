using Finbuckle.MultiTenant;
using Juice.MultiTenant.Domain.AggregatesModel.TenantAggregate;
using Juice.MultiTenant.Shared.Enums;
using Juice.Services;
using Microsoft.EntityFrameworkCore;

namespace Juice.MultiTenant.EF.Stores
{
    public class MultiTenantEFCoreStore<TTenantInfo> : IMultiTenantStore<TTenantInfo>
        where TTenantInfo : class, ITenant, ITenantInfo, new()
    {
        internal readonly TenantStoreDbContext dbContext;
        internal readonly IStringIdGenerator _idGenerator;

        public MultiTenantEFCoreStore(TenantStoreDbContext dbContext, IStringIdGenerator idGenerator)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        }

        public virtual async Task<TTenantInfo?> TryGetAsync(string id)
        {
            return await dbContext.TenantInfo.AsNoTracking()
                    .Where(ti => ti.Id == id)
                    .Select(ti => new TenantInfo(ti.Id, ti.Identifier, ti.Name, ti.SerializedProperties, ti.ConnectionString, ti.OwnerUser))
                    .SingleOrDefaultAsync() as TTenantInfo;
        }

        public virtual async Task<IEnumerable<TTenantInfo>> GetAllAsync()
        {
            return (await dbContext.TenantInfo.AsNoTracking()
                .Select(ti => new TenantInfo(ti.Id, ti.Identifier, ti.Name, ti.SerializedProperties, ti.ConnectionString, ti.OwnerUser))
                .ToListAsync())
                .Select(ti => (ti as TTenantInfo)!)
                .ToList();
        }

        public virtual async Task<TTenantInfo?> TryGetByIdentifierAsync(string identifier)
        {
            return await dbContext.TenantInfo.AsNoTracking()
                .Where(ti => ti.Identifier == identifier && ti.Status == TenantStatus.Active)
                .Select(ti => new TenantInfo(ti.Id, ti.Identifier, ti.Name, ti.SerializedProperties, ti.ConnectionString, ti.OwnerUser))
                .SingleOrDefaultAsync() as TTenantInfo;
        }

        public virtual async Task<bool> TryAddAsync(TTenantInfo tenantInfo)
        {
            var tenant = (ITenantInfo)tenantInfo;
            var id = tenant.Id ?? _idGenerator.GenerateUniqueId();
            var entity = new Tenant
            {
                Id = id,
                Identifier = tenant.Identifier ?? id,
                Name = tenant.Name ?? id,
                ConnectionString = tenantInfo.ConnectionString
            };
            await dbContext.TenantInfo.AddAsync(entity);
            var result = await dbContext.SaveChangesAsync() > 0;
            dbContext.Entry(tenantInfo).State = EntityState.Detached;

            return result;
        }

        public virtual async Task<bool> TryRemoveAsync(string identifier)
        {
            var existing = await dbContext.TenantInfo
                .Where(ti => ti.Identifier == identifier)
                .SingleOrDefaultAsync();

            if (existing is null)
            {
                return false;
            }

            dbContext.TenantInfo.Remove(existing);
            return await dbContext.SaveChangesAsync() > 0;
        }

        public virtual async Task<bool> TryUpdateAsync(TTenantInfo tenantInfo)
        {
            var tenant = (ITenantInfo)tenantInfo;
            var entity = await dbContext.TenantInfo
                 .Where(ti => ti.Id == tenant.Id)
                 .SingleOrDefaultAsync();
            if (entity is null)
            {
                return false;
            }
            if (entity.Identifier != tenant.Identifier)
            {
                entity.Identifier = tenant.Identifier;
            }
            if (entity.Name != tenant.Name)
            {
                entity.Name = tenant.Name ?? "";
            }
            if (entity.ConnectionString != tenantInfo.ConnectionString)
            {
                entity.ConnectionString = tenantInfo.ConnectionString;
            }

            var result = await dbContext.SaveChangesAsync() > 0;
            dbContext.Entry(tenantInfo).State = EntityState.Detached;
            return result;
        }
    }
}
