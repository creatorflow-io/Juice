using System.Net.Mime;
using Juice.AspNetCore.Mvc.Filters;
using Juice.MultiTenant.Shared.Authorization;
using Juice.MultiTenant.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Juice.MultiTenant.Api.Controllers
{

    /// <summary>
    /// Operate tenant
    /// </summary>
    [ApiVersion("1.0")]
    [ApiVersion("2.0")]
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [IgnoreAntiforgeryToken]
    [RequireTenant]
    public class OperationController : ControllerBase
    {

        /// <summary>
        /// Get tenant info
        /// </summary>
        /// <param name="dbContext"></param>
        /// <param name="tenant"></param>
        /// <returns></returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Authorize(Policies.TenantOperationPolicy)]

        public async Task<ActionResult<TenantBasicModel>> GetAsync(
            [FromServices] TenantStoreDbContext dbContext,
            [FromServices] ITenant tenant)
        {
            var model = await dbContext
                .TenantInfo.AsNoTracking()
                .Where(ti => ti.Id == tenant.Id)
                .Select(ti => new TenantBasicModel(
                    ti.Id, ti.Name, ti.Identifier, ti.Status
                ))
                .FirstOrDefaultAsync();
            if (model == null)
            {
                return NotFound();
            }

            return Ok(model);
        }


        /// <summary>
        /// Update tenant info
        /// </summary>
        /// <param name="mediator"></param>
        /// <param name="model"></param>
        /// <param name="tenant"></param>
        /// <returns></returns>
        [HttpPut]
        [Consumes(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Authorize(Policies.TenantOperationPolicy)]

        public async Task<ActionResult> UpdateAsync(
            [FromServices] IMediator mediator,
            [FromBody] TenantBasicUpdateModel model,
            [FromServices] ITenant tenant)
        {

            var command = new UpdateTenantCommand(tenant.Id, model.Identifier,
                model.Name);

            var result = await mediator.Send(command);
            if (result.Succeeded)
            {
                return Ok();
            }
            return BadRequest(result.Message);

        }

        /// <summary>
        /// Delete tenant
        /// </summary>
        /// <param name="mediator"></param>
        /// <param name="tenant"></param>
        /// <returns></returns>
        [HttpDelete]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Authorize(Policies.TenantDeletePolicy)]
        public async Task<ActionResult> DeleteAsync(
            [FromServices] IMediator mediator,
            [FromServices] ITenant tenant)
        {

            var command = new DeleteTenantCommand(tenant.Id);
            var result = await mediator.Send(command);
            if (result.Succeeded)
            {
                return string.IsNullOrEmpty(result.Message) ? Ok() : Ok(result.Message);
            }
            return BadRequest(result.Message);

        }


        /// <summary>
        /// Deactivate tenant
        /// </summary>
        /// <param name="mediator"></param>
        /// <param name="tenant"></param>
        /// <returns></returns>
        [HttpPost("deactivate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Authorize(Policies.TenantOperationPolicy)]

        public async Task<ActionResult> DeactivateAsync(
            [FromServices] IMediator mediator,
           [FromServices] ITenant tenant)
        {
            var command = new AbandonTenantCommand(tenant.Id);
            var result = await mediator.Send(command);
            if (result.Succeeded)
            {
                return string.IsNullOrEmpty(result.Message) ? Ok() : Ok(result.Message);
            }
            return BadRequest(result.Message);
        }


        /// <summary>
        /// Suspend tenant
        /// </summary>
        /// <param name="mediator"></param>
        /// <param name="tenant"></param>
        /// <returns></returns>
        [HttpPost("suspend")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Authorize(Policies.TenantOperationPolicy)]

        public async Task<ActionResult> SuspendAsync(
            [FromServices] IMediator mediator,
            [FromServices] ITenant tenant)
        {
            var command = new OperationStatusCommand(tenant.Id, TenantStatus.Suspended);
            var result = await mediator.Send(command);
            if (result.Succeeded)
            {
                return string.IsNullOrEmpty(result.Message) ? Ok() : Ok(result.Message);
            }
            return BadRequest(result.Message);

        }


        /// <summary>
        /// Reactivate tenant when it is suspended or deactivated
        /// </summary>
        /// <param name="mediator"></param>
        /// <param name="tenant"></param>
        /// <returns></returns>
        [HttpPost("reactivate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Authorize(Policies.TenantOperationPolicy)]

        public async Task<ActionResult> ReactivateAsync(
            [FromServices] IMediator mediator,
            [FromServices] ITenant tenant)
        {
            var command = new OperationStatusCommand(tenant.Id, TenantStatus.Active);
            var result = await mediator.Send(command);
            if (result.Succeeded)
            {
                return string.IsNullOrEmpty(result.Message) ? Ok() : Ok(result.Message);
            }
            return BadRequest(result.Message);

        }


        /// <summary>
        /// Transfer tenant ownership to another user
        /// </summary>
        /// <param name="mediator"></param>
        /// <param name="model"></param>
        /// <param name="tenant"></param>
        /// <returns></returns>
        [HttpPost("transfer")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Authorize(Policies.TenantOwnerPolicy)]
        public async Task<ActionResult> TransferAsync(
            [FromServices] IMediator mediator,
            [FromBody] TenantOwnerTransferModel model,
            [FromServices] ITenant tenant)
        {
            var command = new TransferOwnershipCommand(tenant.Id, model.NewOwner);
            var result = await mediator.Send(command);
            if (result.Succeeded)
            {
                return string.IsNullOrEmpty(result.Message) ? Ok() : Ok(result.Message);
            }
            return BadRequest(result.Message);

        }
    }
}
