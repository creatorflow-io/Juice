using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Juice.EF.MultiTenant.Extensions
{
    public static class MultiTenantDbContextExtensions
    {
        /// <summary>
        /// Enforce tenant rules:
        /// - Root tenant (TenantInfo == null): can add/modify/delete entities annotated <see cref="SharingType.Global"/> from any tenant; must NOT overwrite TenantId.
        /// - Tenant (TenantInfo != null): can add/modify/delete entities only when TenantId == TenantInfo.MessageId.
        /// </summary>
        public static void EnforceTenantPolicies<TContext>(this TContext context)
            where TContext : DbContext, IMultiTenantDbContext
        {
            var tenantInfo = context.TenantInfo;
            var ctxTid = tenantInfo?.Id;
            var mismatchMode = context.TenantMismatchMode;
            var notSetMode = context.TenantNotSetMode;

            var entries = context.ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Where(e => e.Metadata.IsMultiTenant(out var _))
                .ToList();

            if (entries.Count == 0) return;

            // Root tenant behavior: only SharingType.Global allowed; never modify TenantId.
            if (tenantInfo is null)
            {
                var nonGlobal = entries.Where(e => 
                    e.Metadata.IsMultiTenant(out var st) && st != SharingType.Global
                    && string.IsNullOrEmpty((string?)e.Property("TenantId").CurrentValue) == false
                    ).ToList();
                if (nonGlobal.Count != 0 && mismatchMode == TenantMismatchMode.Throw)
                {
                    throw new MultiTenantException($"{nonGlobal.Count} entity(ies) not allowed for root tenant (require SharingType.Global).");
                }
                foreach (var e in nonGlobal.Where(e => e.State == EntityState.Modified))
                {
                    // prevent unintended TenantId change on modify
                    e.Property("TenantId").IsModified = false;
                }
                // Root: allow Global entities regardless of TenantId; do not overwrite TenantId
                // Root: ignore not-set handling (no changes to TenantId)
                return;
            }

            // Tenant behavior: allow only same-tenant operations.
            // Handle TenantId not set (null) first for Added.
            var notSet = entries
                .Where(e => e.State is EntityState.Added)
                .Where(e => string.IsNullOrEmpty((string?)e.Property("TenantId").CurrentValue))
                .ToList();

            if (notSet.Count != 0)
            {
                switch (notSetMode)
                {
                    case TenantNotSetMode.Throw:
                        throw new MultiTenantException($"{notSet.Count} entity(ies) with Tenant Id not set.");
                    case TenantNotSetMode.Overwrite:
                        foreach (var e in notSet)
                        {
                            e.Property("TenantId").CurrentValue = ctxTid;
                        }
                        break;
                }
            }

            // Enforce same-tenant for all states.
            var mismatched = entries
                .Where(e => e.State is EntityState.Modified or EntityState.Deleted)
                .Where(e => (string?)e.Property("TenantId").CurrentValue != ctxTid)
                .ToList();

            if (mismatched.Count != 0)
            {
                switch (mismatchMode)
                {
                    case TenantMismatchMode.Throw:
                        throw new MultiTenantException($"{mismatched.Count} entity(ies) with Tenant Id mismatch.");
                    case TenantMismatchMode.Ignore:
                        // prevent unintended TenantId change on modify
                        foreach (var e in mismatched.Where(e => e.State == EntityState.Modified))
                        {
                            e.Property("TenantId").IsModified = false;
                        }
                        break;
                    case TenantMismatchMode.Overwrite:
                        foreach (var e in mismatched)
                        {
                            e.Property("TenantId").CurrentValue = ctxTid;
                        }
                        break;
                }
            }
        }
    }
}
