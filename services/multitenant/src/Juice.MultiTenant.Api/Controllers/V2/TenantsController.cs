using Juice.AspNetCore.Models;
using Juice.AspNetCore.Mvc.Filters;
using Juice.MultiTenant.Shared.Authorization;
using Juice.MultiTenant.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using EFCore = Microsoft.EntityFrameworkCore.EF;

namespace Juice.MultiTenant.Api.Controllers.V2
{
    /// <summary>
    /// Manage tenants
    /// </summary>

    [ApiVersion("2.0")]
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
        /// <param name="request"></param>
        /// <param name="statuses"></param>
        /// <returns></returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Authorize(Policies.TenantAdminPolicy)]
        public async Task<ActionResult<TableResult<TenantBasicModel>>> ListAsync([FromServices] TenantStoreDbContext dbContext,
           [FromQuery] TableQuery request, [FromQuery] TenantStatus[] statuses)
        {

            request.Standardizing();

            var query = dbContext
                .TenantInfo.AsNoTracking();

            if (!string.IsNullOrEmpty(request.FilterText))
            {
                switch (dbContext.Database.ProviderName)
                {
                    case "Npgsql.EntityFrameworkCore.PostgreSQL":
                        query = query.Where(ti => EFCore.Functions.ILike(ti.Identifier, request.FilterText)
                        || EFCore.Functions.ILike(ti.Name, request.FilterText));
                        break;
                    case "Microsoft.EntityFrameworkCore.SqlServer":
                        query = query.Where(ti => EFCore.Functions.Like(ti.Identifier, request.FilterText)
                        || EFCore.Functions.Like(ti.Name, request.FilterText));
                        break;
                    default:
                        query = query.Where(ti => ti.Identifier.Contains(request.Query)
                        || ti.Name.Contains(request.Query));
                        break;
                }
            }
            if (statuses.Any())
            {
                query = query.Where(ti => statuses.Contains(ti.Status));
            }

            var count = await query.CountAsync();

            var result = await request.ToTableResultAsync(
                query.Select(ti => new TenantBasicModel
                {
                    Id = ti.Id,
                    Name = ti.Name,
                    Identifier = ti.Identifier,
                    Status = ti.Status
                }), HttpContext.RequestAborted
                );

            return Ok(result);
        }


    }
}
