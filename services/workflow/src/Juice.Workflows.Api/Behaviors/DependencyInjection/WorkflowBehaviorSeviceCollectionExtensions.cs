﻿using Juice.Workflows.Api.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Workflows.Api
{
    public static class WorkflowBehaviorSeviceCollectionExtensions
    {
        public static IServiceCollection AddWorkflowStateTransactionBehavior(this IServiceCollection services)
        {
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(WorkflowStateTransactionBehavior<,>));
            return services;
        }
    }
}
