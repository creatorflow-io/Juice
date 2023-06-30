using System.Net.Mime;
using Juice.MultiTenant.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Juice.MultiTenant.Api.Controllers
{
    /// <summary>
    /// Manage tenant settings
    /// </summary>
    [ApiVersion("1.0")]
    [ApiVersion("2.0")]
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [IgnoreAntiforgeryToken]
    public class TenantSettingsController : ControllerBase
    {

        /// <summary>
        /// Get all settings by tenant and inherited settings from default tenant.
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="settingsDbContext"></param>
        /// <param name="tenant"></param>
        /// <returns></returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Authorize(Policies.TenantSettingsPolicy)]
        public async Task<ActionResult<IEnumerable<TenantSettingsModel>>> GetAsync(
            [FromServices] ITenantSettingsRepository repository,
            [FromServices] TenantSettingsDbContext settingsDbContext,
            [FromServices] ITenant? tenant = null
            )
        {

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
        [HttpPut("all/{section?}")]
        [Consumes(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Authorize(Policies.TenantSettingsPolicy)]
        public async Task<ActionResult> UpdateAsync(
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
        [HttpPut("{section}")]
        [Consumes(MediaTypeNames.Text.Plain)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Authorize(Policies.TenantSettingsPolicy)]
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
        [HttpDelete("{section}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Authorize(Policies.TenantSettingsPolicy)]
        public async Task<ActionResult> DeleteAsync(
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
