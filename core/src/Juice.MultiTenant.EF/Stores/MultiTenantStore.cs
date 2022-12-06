namespace Juice.MultiTenant.EF.Stores
{
    //internal class MultiTenantStore<TTenantInfo> : IMultiTenantStore<TTenantInfo>
    //    where TTenantInfo : class, ITenantInfo, new()
    //{
    //    internal readonly TenantDbContext dbContext;

    //    public MultiTenantStore(TenantDbContext dbContext)
    //    {
    //        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    //    }

    //    public virtual async Task<TTenantInfo?> TryGetAsync(string id)
    //    {
    //        return await dbContext.Tenants
    //                        .Where(ti => ti.Id == id)
    //                        .SingleOrDefaultAsync()
    //                        as TTenantInfo
    //                        ;
    //    }

    //    public virtual async Task<IEnumerable<TTenantInfo>> GetAllAsync()
    //    {
    //        return (await dbContext.Tenants.ToListAsync())
    //            .OfType<TTenantInfo>()
    //            ;
    //    }

    //    public virtual async Task<TTenantInfo?> TryGetByIdentifierAsync(string identifier)
    //    {
    //        var tenant = await dbContext.Tenants
    //                        .Where(ti => ti.Identifier == identifier)
    //                        .SingleOrDefaultAsync();
    //        //return tenant;
    //        var rt1 = tenant as TTenantInfo;
    //        return rt1;
    //    }

    //    public virtual async Task<bool> TryAddAsync(TTenantInfo tenantInfo)
    //    {
    //        //await dbContext.Tenants.AddAsync(tenantInfo);

    //        return await dbContext.SaveChangesAsync() > 0;
    //    }

    //    public virtual async Task<bool> TryRemoveAsync(string identifier)
    //    {
    //        var existing = await dbContext.Tenants
    //            .Where(ti => ti.Identifier == identifier)
    //            .SingleOrDefaultAsync();

    //        if (existing is null)
    //        {
    //            return false;
    //        }

    //        dbContext.Tenants.Remove(existing);
    //        return await dbContext.SaveChangesAsync() > 0;
    //    }

    //    public virtual async Task<bool> TryUpdateAsync(TTenantInfo tenantInfo)
    //    {
    //        var existingLocal = dbContext.Tenants.Local.Where(ti => ti.Id == tenantInfo.Id).SingleOrDefault();
    //        if (existingLocal != null)
    //        {
    //            dbContext.Entry(existingLocal).State = EntityState.Detached;
    //        }

    //        //dbContext.Tenants.Update(tenantInfo);
    //        return await dbContext.SaveChangesAsync() > 0;
    //    }
    //}
}
