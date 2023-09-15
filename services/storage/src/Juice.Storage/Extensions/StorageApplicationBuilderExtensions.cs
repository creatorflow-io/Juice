using Juice.Storage.Middleware;
using Microsoft.AspNetCore.Builder;

namespace Juice.Storage.Extensions
{
    public static class StorageApplicationBuilderExtensions
    {
        /// <summary>
        /// You may need to add the .WithExposedHeaders("x-offset", "x-completed") to the CORS policy
        /// </summary>
        /// <param name="app"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseStorage(
            this IApplicationBuilder builder, Action<StorageMiddlewareOptions>? configure = default)
        {
            var options = new StorageMiddlewareOptions();
            configure?.Invoke(options);
            return builder.UseMiddleware<StorageMiddleware>(options);
        }

        /// <summary>
        /// You may need to add the .WithExposedHeaders("x-offset", "x-completed") to the CORS policy
        /// </summary>
        /// <param name="app"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static WebApplication UseStorage(
            this WebApplication app, Action<StorageMiddlewareOptions>? configure = default)
        {
            var options = new StorageMiddlewareOptions();
            configure?.Invoke(options);
            app.UseMiddleware<StorageMiddleware>(options);
            return app;
        }
    }
}
