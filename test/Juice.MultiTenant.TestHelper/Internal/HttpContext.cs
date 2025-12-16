using System.Security.Claims;
using Juice.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Juice.MultiTenant.TestHelper.Internal
{
    internal class MyHttpContext : HttpContext
    {
        private readonly IServiceProvider _serviceProvider;
        private CancellationTokenSource _abortedCts = new();
        public MyHttpContext(IServiceProvider serviceProvider, string? tenantIdentifier)
        {
            _serviceProvider = serviceProvider;
            Items["__TenantIdentifier"] = tenantIdentifier;
        }

        public override IFeatureCollection Features => throw new NotImplementedException();

        public override HttpRequest Request => throw new NotImplementedException();

        public override HttpResponse Response => throw new NotImplementedException();

        public override ConnectionInfo Connection => throw new NotImplementedException();

        public override WebSocketManager WebSockets => throw new NotImplementedException();

        public override ClaimsPrincipal User { get; set; } = new ClaimsPrincipal();
        public override IDictionary<object, object?> Items { get; set; } = new Dictionary<object, object?>();
        public override IServiceProvider RequestServices { get => _serviceProvider; set => throw new NotImplementedException(); }
        public override CancellationToken RequestAborted { get => _abortedCts.Token; set { _abortedCts = CancellationTokenSource.CreateLinkedTokenSource(_abortedCts.Token, value); } }
        public override string TraceIdentifier { get; set; } = new DefaultStringIdGenerator().GenerateUniqueId();
        public override ISession Session { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Abort() => _abortedCts.Cancel();
    }
}
