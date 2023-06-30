using System.Net.Mime;
using Juice.AspNetCore.Mvc.Filters;
using Juice.MultiTenant.Api.Identity;
using Juice.MultiTenant.Api.Mvc.Filters;
using Juice.MultiTenant.Shared.Authorization;
using Juice.MultiTenant.Shared.Enums;
using Juice.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Juice.MultiTenant.Api.Controllers
{

    /// <summary>
    /// Manage tenants
    /// </summary>
    [ApiVersion("1.0")]
    [ApiVersion("2.0")]
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [IgnoreAntiforgeryToken]
    [RootTenant]
    public class AdminController : ControllerBase
    {

        /// <summary>
        /// Get tenant info
        /// </summary>
        /// <param name="dbContext"></param>
        /// <param name="ownerResolver"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}", Name = "GetTenant")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Authorize(Policies.TenantAdminPolicy)]
        public async Task<ActionResult<TenantModel>> GetAsync(
            [FromServices] TenantStoreDbContext dbContext,
            [FromServices] IOwnerResolver ownerResolver,
            [FromRoute] string id)
        {

            var model = await dbContext
                .TenantInfo.AsNoTracking()
                .Where(ti => ti.Id == id || ti.Identifier == id)
                .Select(ti => new TenantModel(
                    ti.Id, ti.Name, ti.Identifier, ti.ConnectionString, ti.Status,
                    ti.OwnerUser,
                    ti.SerializedProperties
                ))
                .FirstOrDefaultAsync();
            if (model == null)
            {
                return NotFound();
            }
            if (!string.IsNullOrEmpty(model.OwnerUser))
            {
                var owner = await ownerResolver.GetOwnerNameAsync(model.OwnerUser);
                if (!string.IsNullOrEmpty(owner))
                {
                    model.SetOwnerName(owner);
                }
            }
            return Ok(model);
        }


        /// <summary>
        /// Create new tenant
        /// </summary>
        /// <param name="mediator"></param>
        /// <param name="idGenerator"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        [Consumes(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Authorize(Policies.TenantAdminPolicy)]
        public async Task<ActionResult<CreatedModel>> CreateAsync(
            [FromServices] IMediator mediator,
            [FromServices] IStringIdGenerator idGenerator,
            [FromBody] TenantCreateModel model)
        {

            var id = idGenerator.GenerateUniqueId();

            var properties = model.Properties ?? new Dictionary<string, string?>();
            properties["AdminUser"] = model.AdminUser;
            properties["AdminEmail"] = model.AdminEmail;

            var command = new CreateTenantCommand(id, model.Identifier, model.Name,
                model.ConnectionString, properties);

            var result = await mediator.Send(command);
            if (result.Succeeded)
            {
                return CreatedAtRoute("GetTenant", new { id },
                    new CreatedModel { Id = id, Identifier = model.Identifier }
                    );
            }
            return BadRequest(result.Message);

        }


        /// <summary>
        /// Update tenant info
        /// </summary>
        /// <param name="mediator"></param>
        /// <param name="model"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPut("{id}")]
        [Consumes(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Authorize(Policies.TenantAdminPolicy)]
        public async Task<ActionResult> UpdateAsync(
            [FromServices] IMediator mediator,
            [FromBody] TenantUpdateModel model,
            [FromRoute] string id
            )
        {
            var command = new UpdateTenantCommand(id, model.Identifier,
                            model.Name, model.ConnectionString);

            var result = await mediator.Send(command);
            if (result.Succeeded)
            {
                return Ok();
            }
            return BadRequest(result.Message);

        }


        /// <summary>
        /// Update tenant properties
        /// </summary>
        /// <param name="mediator"></param>
        /// <param name="model"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPut("{id}/properties")]
        [Consumes(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Authorize(Policies.TenantAdminPolicy)]
        public async Task<ActionResult> UpdatePropertiesAsync(
            [FromServices] IMediator mediator,
            [FromBody] TenantUpdatePropertiesModel model,
            [FromRoute] string id
            )
        {
            var command = new UpdateTenantPropertiesCommand(id,
                model.Properties ?? new Dictionary<string, string>());

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
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Authorize(Policies.TenantDeletePolicy)]
        public async Task<ActionResult> DeleteAsync(
            [FromServices] IMediator mediator,
            [FromRoute] string id)
        {

            var command = new DeleteTenantCommand(id);
            var result = await mediator.Send(command);
            if (result.Succeeded)
            {
                return string.IsNullOrEmpty(result.Message) ? Ok() : Ok(result.Message);
            }
            return BadRequest(result.Message);

        }


        /// <summary>
        /// Activate tenant in the first time
        /// </summary>
        /// <param name="mediator"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost("{id}/activate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Authorize(Policies.TenantAdminPolicy)]
        public async Task<ActionResult> ActivateAsync(
            [FromServices] IMediator mediator,
            [FromRoute] string id)
        {

            var command = new AdminStatusCommand(id, TenantStatus.Active);
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
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost("{id}/deactivate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Authorize(Policies.TenantAdminPolicy)]
        public async Task<ActionResult> DeactivateAsync(
            [FromServices] IMediator mediator,
           [FromRoute] string id)
        {

            var command = new AdminStatusCommand(id, TenantStatus.Inactive);
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
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost("{id}/suspend")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Authorize(Policies.TenantAdminPolicy)]
        public async Task<ActionResult> SuspendAsync(
            [FromServices] IMediator mediator,
            [FromRoute] string id)
        {

            var command = new AdminStatusCommand(id, TenantStatus.Suspended);
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
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost("{id}/reactivate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Authorize(Policies.TenantAdminPolicy)]
        public async Task<ActionResult> ReactivateAsync(
            [FromServices] IMediator mediator,
            [FromRoute] string id)
        {
            var command = new OperationStatusCommand(id, TenantStatus.Active);
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
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost("{id}/transfer")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Authorize(Policies.TenantAdminPolicy)]
        public async Task<ActionResult> TransferAsync(
            [FromServices] IMediator mediator,
            [FromBody] TenantOwnerTransferModel model,
            [FromRoute] string id)
        {

            var command = new TransferOwnershipCommand(id, model.NewOwner);
            var result = await mediator.Send(command);
            if (result.Succeeded)
            {
                return string.IsNullOrEmpty(result.Message) ? Ok() : Ok(result.Message);
            }
            return BadRequest(result.Message);

        }


        /// <summary>
        /// Abandon tenant
        /// </summary>
        /// <param name="mediator"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPut("{id}/abandon")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Authorize(Policies.TenantAdminPolicy)]
        public async Task<ActionResult> AbandonAsync(
            [FromServices] IMediator mediator,
            [FromRoute] string id)
        {
            var command = new AbandonTenantCommand(id);
            var result = await mediator.Send(command);
            if (result.Succeeded)
            {
                return string.IsNullOrEmpty(result.Message) ? Ok() : Ok(result.Message);
            }
            return BadRequest(result.Message);

        }


        /// <summary>
        /// Approve tenant when it is created by a user
        /// </summary>
        /// <param name="mediator"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost("{id}/approve")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Authorize(Policies.TenantAdminPolicy)]
        public async Task<ActionResult> ApproveAsync(
            [FromServices] IMediator mediator,
            [FromRoute] string id)
        {
            var command = new ApprovalProcessCommand(id, TenantStatus.Approved);
            var result = await mediator.Send(command);
            if (result.Succeeded)
            {
                return string.IsNullOrEmpty(result.Message) ? Ok() : Ok(result.Message);
            }
            return BadRequest(result.Message);

        }

        /// <summary>
        /// Reject tenant when it is created by a user
        /// </summary>
        /// <param name="mediator"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost("{id}/reject")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Authorize(Policies.TenantAdminPolicy)]
        public async Task<ActionResult> RejectAsync(
            [FromServices] IMediator mediator,
            [FromRoute] string id)
        {
            var command = new ApprovalProcessCommand(id, TenantStatus.Approved);
            var result = await mediator.Send(command);
            if (result.Succeeded)
            {
                return string.IsNullOrEmpty(result.Message) ? Ok() : Ok(result.Message);
            }
            return BadRequest(result.Message);

        }


        /// <summary>
        /// Get all settings by tenant and inherited settings from default tenant.
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="tenantDbContext"></param>
        /// <param name="settingsDbContext"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}/settings")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Authorize(Policies.TenantSettingsPolicy)]
        [AdminSetting]
        public async Task<ActionResult<IEnumerable<TenantSettingsModel>>> SettingsAsync(
            [FromRoute] string id,
            [FromServices] ITenantSettingsRepository repository,
            [FromServices] TenantStoreDbContext tenantDbContext,
            [FromServices] TenantSettingsDbContext settingsDbContext
            )
        {
            var tenant = await tenantDbContext.TenantInfo.FindAsync(new object[] { id }, HttpContext.RequestAborted);
            if (tenant == null)
            {
                return NotFound();
            }

            var rootSettings = await settingsDbContext.TenantSettings
                .Where(s => string.IsNullOrEmpty(s.TenantId))
                .ToListAsync(HttpContext.RequestAborted);

            var settings = await repository.GetAllAsync(HttpContext.RequestAborted);

            var models = settings.Select(setting => new TenantSettingsModel
            {
                Key = setting.Key,
                Value = setting.Value,
                Inherited = tenant != null && (string.IsNullOrEmpty(setting.TenantId)
                    || !setting.TenantId.Equals(tenant.Id, StringComparison.OrdinalIgnoreCase)),
                Overridden = tenant != null && setting.TenantId.Equals(tenant.Id, StringComparison.OrdinalIgnoreCase)
                  && rootSettings.Any(s => s.Key == setting.Key)
            }).OrderBy(s => s.Key).ToArray();
            return Ok(models);
        }

        /// <summary>
        /// Update all settings by tenant if no section is specified or update settings with specified section. 
        /// If setting is inherited, it will be ignored. 
        /// If current settings does not match with new settings, it will be updated or removed.
        /// </summary>
        /// <param name="mediator"></param>
        /// <param name="model"></param>
        /// <param name="section"></param>
        /// <returns></returns>
        [HttpPut("{id}/settings/{section?}")]
        [Consumes(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Authorize(Policies.TenantSettingsPolicy)]
        [AdminSetting]
        public async Task<ActionResult> UpdateSettingsAsync(
            [FromServices] IMediator mediator,
            [FromBody] IEnumerable<TenantSettingsModel> model,
            [FromRoute] string? section = null
        )
        {
            var settings = model.Where(s => !s.Inherited)
                .ToDictionary(s => s.Key, s => s.Value?.ToString());

            var command = new UpdateSettingsCommand(section ?? "", settings);
            var result = await mediator.Send(command);
            if (result.Succeeded)
            {
                return Ok();
            }
            return BadRequest(result.Message);
        }

        /// <summary>
        /// Update setting with specified section
        /// </summary>
        /// <param name="mediator"></param>
        /// <param name="section">Setting Key like Logger:Enabled</param>
        /// <param name="value">Setting Value like False</param>
        /// <returns></returns>
        [HttpPut("{id}/settings/section/{section}")]
        [Consumes(MediaTypeNames.Text.Plain)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Authorize(Policies.TenantSettingsPolicy)]
        [AdminSetting]
        public async Task<ActionResult> UpdateSectionAsync(
            [FromServices] IMediator mediator,
            [FromRoute] string section,
            [FromBody] string? value
        )
        {
            var command = new UpdateSettingsCommand(section, new Dictionary<string, string?> { { "", value } });
            var result = await mediator.Send(command);
            if (result.Succeeded)
            {
                return Ok();
            }
            return BadRequest(result.Message);
        }

        /// <summary>
        /// Delete tenant settings with specified section
        /// </summary>
        /// <param name="mediator"></param>
        /// <param name="section"></param>
        /// <returns></returns>
        [HttpDelete("{id}/settings/{section}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Authorize(Policies.TenantSettingsPolicy)]
        [AdminSetting]
        public async Task<ActionResult> DeleteSectionAsync(
            [FromServices] IMediator mediator,
            [FromRoute] string section
        )
        {
            var command = new DeleteSettingsCommand(section);
            var result = await mediator.Send(command);
            if (result.Succeeded)
            {
                return Ok();
            }
            return BadRequest(result.Message);
        }
    }
}
