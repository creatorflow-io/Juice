using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Modular
{
    public interface IModuleStartup
    {
        int StartOrder { get; }
        int ConfigureOrder { get; }

        void ConfigureServices(IServiceCollection services, IMvcBuilder mvc, IWebHostEnvironment env, IConfiguration configuration);
        void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IWebHostEnvironment env);
    }

}
