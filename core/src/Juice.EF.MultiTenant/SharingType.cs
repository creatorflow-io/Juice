using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Juice.EF.MultiTenant
{
    public enum SharingType
    {
        None,
        /// <summary>
        /// The entity without tenantId (global entity) should be shared with all tenants
        /// <para>Entity.TenantId == DbContext.TenantInfo.Id or (Entity.TenantId == null (or empty))</para>
        /// </summary>
        Tenant,
        /// <summary>
        /// The entity with tenantId should be shared with master tenant (global tenant)
        /// <para>Entity.TenantId == DbContext.TenantInfo.Id or (DbContext.TenantInfo.Id == null (or empty))</para>
        /// </summary>
        Global
    }
}
