using Juice.Workflows.Api.Grpc.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Juice.Workflows.Api
{
    public static class WfApplicationBuilderExtensions
    {
        /// <summary>
        /// Map workflow gRPC services
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IEndpointRouteBuilder MapWorkflowGrpcServices(this IEndpointRouteBuilder builder)
        {
            builder.MapGrpcService<WorkflowService>();

            return builder;
        }
    }
}
