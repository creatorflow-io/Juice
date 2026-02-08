namespace Juice.EF.MultiTenant
{
    public enum SharingType
    {
        /// <summary>
        /// The entity is not shared between tenants, so each entity must have its own tenantId
        /// </summary>
        None,
        /// <summary>
        /// The entity without tenantId (global entity) can be read from tenants but cannot be modified or deleted
        /// <para>Entity.TenantId == DbContext.TenantInfo.MessageId or (Entity.TenantId == null (or empty))</para>
        /// </summary>
        Tenant,
        /// <summary>
        /// The entity with tenantId can be read/modified/deleted by master tenant
        /// <para>Entity.TenantId == DbContext.TenantInfo.MessageId or (DbContext.TenantInfo.MessageId == null (or empty))</para>
        /// </summary>
        Global
    }
}
