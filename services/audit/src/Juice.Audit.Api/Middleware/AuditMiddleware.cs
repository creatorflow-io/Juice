using System.Diagnostics;
using System.Reflection;
using System.Security.Claims;
using Juice.Audit.Domain.AccessLogAggregate;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Juice.Audit.AspNetCore.Middleware
{
    public class AuditMiddleware
    {
        private RequestDelegate _next;
        private string _appName;
        private AuditFilterOptions _filter;

        public AuditMiddleware(RequestDelegate next, string appName, AuditFilterOptions options)
        {
            _next = next;
            _appName = appName;
            _filter = options;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            using var auditContextAccessor = context.RequestServices.GetRequiredService<IAuditContextAccessor>();
            var logger = context.RequestServices.GetRequiredService<ILogger<AuditMiddleware>>();

            var totalTimeTracker = new Stopwatch();
            var timeTracker = new Stopwatch();
            var dbg = logger.IsEnabled(LogLevel.Debug);
            timeTracker.Start();
            totalTimeTracker.Start();

            try
            {
                InitAuditContext(auditContextAccessor, context);
                if (dbg)
                {
                    logger.LogDebug("AuditMiddleware.InvokeAsync: InitAuditContext {0}", timeTracker.ElapsedMilliseconds);
                    timeTracker.Restart();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error while initializing the audit context");
            }

            try
            {
                CollectRequestInfo(auditContextAccessor, context);
                if (dbg)
                {
                    logger.LogDebug("AuditMiddleware.InvokeAsync: CollectRequestInfo {0}", timeTracker.ElapsedMilliseconds);
                    timeTracker.Restart();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error while collecting the request information");
            }

            try
            {
                CollectServerInfo(auditContextAccessor);
                if (dbg)
                {
                    logger.LogDebug("AuditMiddleware.InvokeAsync: CollectServerInfo {0}", timeTracker.ElapsedMilliseconds);
                    timeTracker.Restart();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error while collecting the server information");
            }

            try
            {
                await _next(context);
                if (dbg)
                {
                    logger.LogDebug("AuditMiddleware.InvokeAsync: _next {0}", timeTracker.ElapsedMilliseconds);
                    timeTracker.Restart();
                }

                try
                {
                    CollectResponseInfo(auditContextAccessor, context, totalTimeTracker.ElapsedMilliseconds);
                    if (dbg)
                    {
                        logger.LogDebug("AuditMiddleware.InvokeAsync: CollectResponseInfo {0}", timeTracker.ElapsedMilliseconds);
                        timeTracker.Restart();
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error while collecting the response information");
                }
            }
            catch (Exception ex)
            {
                try
                {
                    CollectResponseInfo(auditContextAccessor, context, totalTimeTracker.ElapsedMilliseconds, ex);
                    if (dbg)
                    {
                        logger.LogDebug("AuditMiddleware.InvokeAsync: CollectResponseError {0}", timeTracker.ElapsedMilliseconds);
                        timeTracker.Restart();
                    }
                }
                catch (Exception ex1)
                {
                    logger.LogWarning(ex1, "Error while collecting the response error");
                }
                throw;
            }
            finally
            {
                try
                {
                    var auditService = context.RequestServices.GetService<IAuditService>();
                    if (dbg)
                    {
                        logger.LogDebug("AuditMiddleware.InvokeAsync: Get IAuditService {0}", timeTracker.ElapsedMilliseconds);
                        timeTracker.Restart();
                    }
                    if (auditService != null && auditContextAccessor.AuditContext?.AccessRecord != null)
                    {
                        await auditService.PersistAuditInformationAsync(auditContextAccessor.AuditContext.AccessRecord,
                            auditContextAccessor.AuditContext.AuditEntries.ToArray(), default);
                    }
                    if (dbg)
                    {
                        logger.LogDebug("AuditMiddleware.InvokeAsync: CommitAuditInformationAsync {0}", timeTracker.ElapsedMilliseconds);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, $"Error while committing audit information");
                }
                timeTracker.Stop();
                totalTimeTracker.Stop();
            }

        }

        private void InitAuditContext(IAuditContextAccessor auditContextAccessor,
            HttpContext context)
        {
            var user = context.User?.FindFirst(ClaimTypes.Name)?.Value;
            var action = context.Request.Path.HasValue
                ? context.Request.Path.Value.Trim('/').Replace("/", "_")
                : "Unknown";

            auditContextAccessor.Init(action, user);

        }

        private void CollectRequestInfo(IAuditContextAccessor auditContextAccessor,
            HttpContext context)
        {
            var requestInfo = new RequestInfo(
                context.Request.Method,
                context.Request.Path,
                context.Request.HasFormContentType ? JsonConvert.SerializeObject(context.Request.Form) : default,
                context.Request.QueryString.HasValue ? context.Request.QueryString.Value : default,
                JsonConvert.SerializeObject(context.Request.Headers
                    .Where(h => _filter.IsReqHeaderMatch(h.Key))
                    .ToDictionary(x => x.Key, x => x.Value)),
                context.Request.Scheme,
                context.Connection.RemoteIpAddress?.ToString(),
                context.TraceIdentifier,
                context.Request.Host.HasValue ? context.Request.Host.Value : default
                )
           ;
            auditContextAccessor.AuditContext?.SetRequestInfo(requestInfo);
        }

        private void CollectServerInfo(IAuditContextAccessor auditContextAccessor)
        {
            var serverInfo = new ServerInfo(
                Environment.MachineName,
                Environment.OSVersion.ToString(),
                Assembly.GetEntryAssembly()?.GetName()
                    ?.Version?.ToString(),
                _appName
                );
            auditContextAccessor.AuditContext?.SetServerInfo(serverInfo);
        }

        private void CollectResponseInfo(IAuditContextAccessor auditContextAccessor,
            HttpContext context, long elapsed, Exception? ex = default)
        {
            auditContextAccessor.AuditContext?.UpdateResponseInfo(responseInfo =>
            {
                if (ex != null)
                {
                    responseInfo.TrySetMessage(ex.Message);
                    responseInfo.TrySetError(ex.StackTrace ?? ex.ToString());
                }
                responseInfo.SetResponseInfo(context.Response.StatusCode,
                    JsonConvert.SerializeObject(context.Response.Headers
                        .Where(h => _filter.IsResHeaderMatch(h.Key))
                        .ToDictionary(x => x.Key, x => x.Value)),
                    elapsed);
            });
        }

    }
}
