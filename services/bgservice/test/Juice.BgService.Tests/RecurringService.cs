using System;
using System.Threading.Tasks;
using Juice.BgService.Scheduled;
using Microsoft.Extensions.Logging;

namespace Juice.BgService.Tests
{
    public class RecurringService : ScheduledService
    {
        public RecurringService(IServiceProvider serviceProvider, ILogger<RecurringService> logger) : base(serviceProvider)
        {
            _logger = logger;
            ScheduleOptions.OccursInterval(TimeSpan.FromSeconds(3));
        }

        public override Task<(bool Healthy, string Message)> HealthCheckAsync() =>
            Task.FromResult((true, ""));

        public override async Task<(bool Succeeded, string? Message)> InvokeAsync()
        {
            using (_logger.BeginScope("Tasks invoke"))
            {
                Console.WriteLine("Hello... next invoke {0} time is {1}. Instances count: {2}", Description, NextProcessing, globalCounter);
                for (var i = 0; i < 10000; i++)
                {
                    if (_stopRequest.IsCancellationRequested) { break; }
                    Console.WriteLine("Task {0}", i);
                }
                Console.WriteLine("End");
            }
            return (true, default);
        }

        protected override void Cleanup()
        {
            Console.WriteLine("Cleanup");
        }
    }
}
