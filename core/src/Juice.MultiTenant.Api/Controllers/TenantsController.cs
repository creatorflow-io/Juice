using Juice.AspNetCore.Mvc.Filters;
using Juice.MultiTenant.Shared.Authorization;
using Juice.MultiTenant.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using EFCore = Microsoft.EntityFrameworkCore.EF;

namespace Juice.MultiTenant.Api.Controllers
{
    /// <summary>
    /// Manage tenants
    /// </summary>
    [ApiVersion("1.0")]
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [IgnoreAntiforgeryToken]
    [RootTenant]
    public class TenantsController : ControllerBase
    {
        /// <summary>
        /// Listing all tenants
        /// </summary>
        /// <param name="dbContext"></param>
        /// <param name="q"></param>
        /// <param name="statuses"></param>
        /// <param name="skip"></param>
        /// <param name="take"></param>
        /// <returns></returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Authorize(Policies.TenantAdminPolicy)]
        public async Task<ActionResult<TenantTableModel>> ListAsync([FromServices] TenantStoreDbContext dbContext,
           [FromQuery] string? q, [FromQuery] TenantStatus[] statuses,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 20)
        {
            var query = dbContext
                .TenantInfo.AsNoTracking();
            if (!string.IsNullOrEmpty(q))
            {
                switch (dbContext.Database.ProviderName)
                {
                    case "Npgsql.EntityFrameworkCore.PostgreSQL":
                        query = query.Where(ti => EFCore.Functions.ILike(ti.Identifier, $"%{q.Trim()}%")
                        || EFCore.Functions.ILike(ti.Name, $"%{q.Trim()}%"));
                        break;
                    case "Microsoft.EntityFrameworkCore.SqlServer":
                        query = query.Where(ti => EFCore.Functions.Like(ti.Identifier, $"%{q.Trim()}%")
                        || EFCore.Functions.Like(ti.Name, $"%{q.Trim()}%"));
                        break;
                    default:
                        query = query.Where(ti => ti.Identifier.Contains(q)
                        || ti.Name.Contains(q));
                        break;
                }
            }
            if (statuses.Any())
            {
                query = query.Where(ti => statuses.Contains(ti.Status));
            }

            take = Math.Max(10, Math.Min(50, take));

            var count = await query.CountAsync();

            var tenants = await query
                .OrderBy(t => t.Name)
                .Skip(skip).Take(take)
                .Select(ti => new TenantBasicModel(
                    ti.Id, ti.Name, ti.Identifier, ti.Status
                    ))
                .ToArrayAsync(HttpContext.RequestAborted);
            return Ok(new TenantTableModel { Count = count, Data = tenants });
        }


    }
}
