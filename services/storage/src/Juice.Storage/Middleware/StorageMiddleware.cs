using Juice.Storage.Abstractions;
using Juice.Storage.Dto;
using Juice.Storage.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;

namespace Juice.Storage.Middleware
{
    public class StorageMiddleware
    {
        private RequestDelegate _next;
        private StorageMiddlewareOptions _options;
        public StorageMiddleware(RequestDelegate next,
            StorageMiddlewareOptions options)
        {
            _next = next;
            _options = options;
        }

        public async Task InvokeAsync(HttpContext context)
        {

            var endpoint = _options?.Endpoint ?? "/storage";

            var path = context.Request.Path.ToString().ToLower();
            if (path.StartsWith(endpoint))
            {
                var action = path.Substring(endpoint.Length);

                switch (action)
                {
                    case "/exists":
                        await InvokeExistsAsync(context);
                        break;
                    case "/init":
                        await InvokeInitAsync(context);
                        break;
                    case "/upload":
                        await InvokeUploadAsync(context);
                        break;
                    case "/complete":
                        await InvokeCompleteAsync(context);
                        break;
                    case "/failure":
                        await InvokeFailureAsync(context);
                        break;
                    default:
                        await _next(context);
                        break;
                }
            }
            else
            {
                await _next(context);
            }
        }

        #region Check file exists
        private string GetFilePathFromForm(HttpContext context)
        {
            var filePath = context.Request.Form["filePath"];
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("filePath is missing in form");
            }
            return filePath;
        }

        private async Task InvokeExistsAsync(HttpContext context)
        {
            if (context.Request.Method != HttpMethod.Post.Method)
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                return;
            }
            try
            {
                var uploadManager = context.RequestServices.GetRequiredService<IUploadManager>();

                var filePath = GetFilePathFromForm(context);

                var exists = await uploadManager.ExistsAsync(filePath, context.RequestAborted);

                context.Response.StatusCode = StatusCodes.Status200OK;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(exists));
            }
            catch (ArgumentException ex)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync(ex.Message);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync(ex.Message);
            }
        }

        #endregion

        #region Init upload
        private InitialFileInfo GetInitialFileInfoFromForm(HttpContext context)
        {

            var filePath = context.Request.Form["filePath"];
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("filePath is missing in form");
            }

            var fileSizeStr = context.Request.Form["fileSize"];
            if (string.IsNullOrWhiteSpace(fileSizeStr) || !long.TryParse(fileSizeStr, out var fileSize))
            {
                throw new ArgumentException("fileSize is missing in form");
            }

            var fileExistsBehaviorStr = context.Request.Form["fileExistsBehavior"];
            if (string.IsNullOrWhiteSpace(fileExistsBehaviorStr)
                || !Enum.TryParse<FileExistsBehavior>(fileExistsBehaviorStr, out var fileExistsBehavior))
            {
                throw new ArgumentException("fileExistsBehavior is missing in form");
            }

            var uploadIdStr = context.Request.Form["uploadId"];
            if (!string.IsNullOrWhiteSpace(fileSizeStr) && Guid.TryParse(uploadIdStr, out var uploadId))
            {
                return new InitialFileInfo(filePath, fileSize, fileExistsBehavior, uploadId);
            }

            return new InitialFileInfo(filePath, fileSize, fileExistsBehavior);

        }

        private async Task InvokeInitAsync(HttpContext context)
        {
            if (context.Request.Method != HttpMethod.Post.Method)
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                return;
            }
            try
            {
                var uploadManager = context.RequestServices.GetRequiredService<IUploadManager>();

                var fileInfo = GetInitialFileInfoFromForm(context);

                var configuration = await uploadManager.InitAsync(fileInfo, context.RequestAborted);

                context.Response.StatusCode = StatusCodes.Status201Created;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(configuration));

            }
            catch (ArgumentException ex)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync(ex.Message);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync(ex.Message);
            }
        }
        #endregion

        #region Upload

        private (Guid uploadId, long offset) GetUploadInfoFromHeaders(HttpContext context)
        {
            var uploadIdStr = context.Request.Headers["x-uploadid"];
            if (string.IsNullOrWhiteSpace(uploadIdStr) || !Guid.TryParse(uploadIdStr, out var uploadId))
            {
                throw new ArgumentException("x-uploadid is missing in the header");
            }

            var offsetStr = context.Request.Headers["x-offset"];
            if (string.IsNullOrEmpty(offsetStr))
            {
                return (uploadId, default);
            }
            if (!long.TryParse(offsetStr, out var offset))
            {
                throw new ArgumentException("x-offset header is invalid");
            }
            return (uploadId, offset);
        }

        private async Task InvokeUploadAsync(HttpContext context)
        {
            var logger = context.RequestServices.GetService<ILogger<StorageMiddleware>>();
            var request = context.Request;

            var boundary = MultipartRequestHelper.GetBoundary(request);

            // validation of Content-Type
            // 1. first, it must be a form-data request
            // 2. a boundary should be found in the Content-Type

            if (!request.HasFormContentType || string.IsNullOrEmpty(boundary))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("No files data in the request");
            }

            var reader = new MultipartReader(boundary, context.Request.Body, 1024 * 1024)
            ;
            var section = await reader.ReadNextSectionAsync();

            try
            {
                var (uploadId, offset) = GetUploadInfoFromHeaders(context);
                logger?.LogInformation("Upload file {uploadId} from offset {offset}", uploadId, offset);
                var uploadManager = context.RequestServices.GetRequiredService<IUploadManager>();

                while (section != null)
                {
                    if (ContentDispositionHeaderValue.TryParse(section.ContentDisposition,
                    out var contentDisposition) && contentDisposition.DispositionType.Equals("form-data") &&
                    !string.IsNullOrEmpty(contentDisposition.FileName.Value))
                    {
                        using var stream = section.Body;
                        var size = await uploadManager.UploadAsync(uploadId, stream, offset, context.RequestAborted);
                        context.Response.StatusCode = StatusCodes.Status200OK;
                        context.Response.Headers.Add("x-offset", size.ToString());
                        await context.Response.WriteAsync(size.ToString());
                        return;
                    }
                    section = await reader.ReadNextSectionAsync();
                }
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
            catch (IOException ex)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync("Failed to write file to storage");
                logger.LogError(ex, ex.Message);
            }
            catch (TaskCanceledException ex)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Client aborted upload");
                logger.LogError(ex, ex.Message);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync(ex.Message);
                logger.LogError(ex, ex.Message);
            }
        }

        #endregion

        #region Completed

        private Guid GetUploadIdFromForm(HttpContext context)
        {
            var uploadIdStr = context.Request.Form["uploadId"];
            if (string.IsNullOrWhiteSpace(uploadIdStr) || !Guid.TryParse(uploadIdStr, out var uploadId))
            {
                throw new ArgumentException("uploadId is missing in the form data");
            }
            return uploadId;
        }

        private async Task InvokeCompleteAsync(HttpContext context)
        {
            try
            {
                var logger = context.RequestServices.GetService<ILogger<StorageMiddleware>>();
                var uploadId = GetUploadIdFromForm(context);
                logger?.LogInformation("Upload completed {uploadId}", uploadId);

                var uploadManager = context.RequestServices.GetRequiredService<IUploadManager>();

                await uploadManager.CompleteAsync(uploadId, context.RequestAborted);

                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync(ex.Message);
            }
        }

        private async Task InvokeFailureAsync(HttpContext context)
        {
            try
            {
                var logger = context.RequestServices.GetService<ILogger<StorageMiddleware>>();
                var uploadId = GetUploadIdFromForm(context);
                logger?.LogInformation("Upload failured {uploadId}", uploadId);

                var uploadManager = context.RequestServices.GetRequiredService<IUploadManager>();

                await uploadManager.FailureAsync(uploadId, context.RequestAborted);

                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync(ex.Message);
            }
        }
        #endregion

        #region Download

        private async Task InvokeDownloadAsync(HttpContext context)
        {
            if (context.Request.Method != HttpMethod.Get.Method)
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                return;
            }
            try
            {
                var uploadManager = context.RequestServices.GetRequiredService<IUploadManager>();

                var filePath = context.Request.Query
                    .Where(q => q.Key.Equals("fileName", StringComparison.OrdinalIgnoreCase))
                    .Select(q => q.Value.ToString())
                    .FirstOrDefault();
                if (string.IsNullOrEmpty(filePath))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                var exists = await uploadManager.ExistsAsync(filePath, context.RequestAborted);
                if (!exists)
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status200OK;
            }
            catch (ArgumentException ex)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync(ex.Message);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync(ex.Message);
            }
        }
        #endregion
    }

    public static class StorageMiddlewareExtensions
    {
        public static IApplicationBuilder UseStorage(
            this IApplicationBuilder builder, Action<StorageMiddlewareOptions>? configure = default)
        {
            var options = new StorageMiddlewareOptions();
            configure?.Invoke(options);
            return builder.UseMiddleware<StorageMiddleware>(options);
        }

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
