using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Modular
{
    public abstract class ModuleStartup : IModuleStartup
    {
        /// <summary>
        /// Default value is 10
        /// </summary>
        public virtual int StartOrder => 10;

        /// <summary>
        /// Default value is StartOrder
        /// </summary>
        public virtual int ConfigureOrder => StartOrder;

        public virtual ValueTask ConfigurePipelineAsync(IApplicationBuilder app, IEndpointRouteBuilder routes, IWebHostEnvironment env)
        {
            return ValueTask.CompletedTask;
        }

        public virtual void ConfigureServices(IServiceCollection services, IMvcBuilder mvc, IWebHostEnvironment env, IConfiguration configuration)
        {
        }

        public virtual ValueTask ShutdownAsync(IServiceProvider serviceProvider, IWebHostEnvironment env)
        {
            return ValueTask.CompletedTask;
        }
    }
}
