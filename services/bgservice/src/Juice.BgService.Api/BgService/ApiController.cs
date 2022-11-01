using Juice.BgService.Api.BgService.Models;
using Juice.BgService.Management;
using Juice.BgService.Management.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Juice.BgService.Api
{
    [ApiController]
    [Route("bgservice/api")]
    public class ApiController : Controller
    {
        private ServiceManager _serviceManager;

        public ApiController(ServiceManager serviceManager)
        {
            _serviceManager = serviceManager;
        }

        /// <summary>
        /// Get service status
        /// </summary>
        /// <returns></returns>
        [HttpGet("[action]")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult Status()
        {
            return Json(new ServiceStatus(
                _serviceManager.Description,
                _serviceManager.State,
                _serviceManager.Message,
                _serviceManager.ManagedServices
                ));
        }

        /// <summary>
        /// Start service if not started.
        /// </summary>
        /// <returns></returns>
        [HttpPost("[action]")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> StartAsync()
        {
            await _serviceManager.StartAsync(default);
            return Ok();
        }

        /// <summary>
        /// Force stop service.
        /// </summary>
        /// <returns></returns>
        [HttpPost("[action]")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> StopAsync()
        {
            await _serviceManager.StopAsync(default);
            return Ok();
        }

        /// <summary>
        /// Force restart service.
        /// </summary>
        /// <returns></returns>
        [HttpPost("[action]")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RestartAsync()
        {
            await _serviceManager.RestartAsync(default);
            return Ok();
        }

        /// <summary>
        /// Request stop service after all jobs completed.
        /// </summary>
        /// <returns></returns>
        [HttpPost("[action]")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RequestStopAsync()
        {
            await _serviceManager.RequestStopAsync(default);
            return Ok();
        }

        /// <summary>
        /// Request restart service after all jobs completed.
        /// </summary>
        /// <returns></returns>
        [HttpPost("[action]")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RequestRestartAsync()
        {
            await _serviceManager.RequestRestartAsync(default);
            return Content("OK");
        }

        #region Sub-Service
        /// <summary>
        /// Start specified sub-service in endpoint.
        /// </summary>
        /// <returns></returns>
        [HttpPost("[action]")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> StartServiceAsync(Guid id)
        {
            await _serviceManager.StartAsync(id, default);
            return Content("OK");
        }

        /// <summary>
        /// Force stop specified sub-service in endpoint.
        /// </summary>
        /// <returns></returns>
        [HttpPost("[action]")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> StopServiceAsync(Guid id)
        {
            await _serviceManager.StopAsync(id, default);
            return Content("OK");
        }

        /// <summary>
        /// Force restart specified sub-service in endpoint.
        /// </summary>
        /// <returns></returns>
        [HttpPost("[action]")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RestartServiceAsync(Guid id)
        {
            await _serviceManager.RestartAsync(id, default);
            return Content("OK");
        }

        /// <summary>
        /// Request stop specified sub-service in endpoint after all jobs completed.
        /// </summary>
        /// <returns></returns>
        [HttpPost("[action]")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RequestStopServiceAsync(Guid id)
        {
            await _serviceManager.RequestStopAsync(id, default);
            return Content("OK");
        }

        /// <summary>
        /// Request restart specified sub-service in endpoint after all jobs completed.
        /// </summary>
        /// <returns></returns>
        [HttpPost("[action]")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RequestRestartServiceAsync(Guid id)
        {
            await _serviceManager.RequestRestartAsync(id, default);
            return Content("OK");
        }
        #endregion

    }
}
