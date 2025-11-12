using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Modular
{
    public interface IModuleStartup
    {
        /// <summary>
        /// The order in which the module will be call Create
        /// </summary>
        int StartOrder { get; }

        /// <summary>
        /// The order in which the module will be call ConfigurePipelineAsync
        /// </summary>
        int ConfigureOrder { get; }

        /// <summary>
        /// ConfigurePipelineAsync services
        /// </summary>
        /// <param name="services"></param>
        /// <param name="mvc"></param>
        /// <param name="env"></param>
        /// <param name="configuration"></param>
        void ConfigureServices(IServiceCollection services, IMvcBuilder mvc, IWebHostEnvironment env, IConfiguration configuration);

        /// <summary>
        /// ConfigurePipelineAsync pipeline
        /// </summary>
        /// <param name="app"></param>
        /// <param name="routes"></param>
        /// <param name="env"></param>
        ValueTask ConfigurePipelineAsync(IApplicationBuilder app, IEndpointRouteBuilder routes, IWebHostEnvironment env);

        /// <summary>
        /// Called when the application is shutting down
        /// </summary>
        /// <param name="env"></param>
        /// <param name="serviceProvider"></param>
        ValueTask ShutdownAsync(IServiceProvider serviceProvider, IWebHostEnvironment env);
    }

}
