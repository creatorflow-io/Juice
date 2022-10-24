using System;
using System.Threading.Tasks;
using Juice.BgService.Scheduled;
using Microsoft.Extensions.Logging;

namespace Juice.BgService.Tests
{
    public class RecurringService : ScheduledService
    {
        public RecurringService(ILogger<RecurringService> logger) : base(logger)
        {
            ScheduleOptions.OccursInterval(TimeSpan.FromSeconds(3));
        }

        public override async Task<(bool Succeeded, string? Message)> InvokeAsync()
        {
            Console.WriteLine("Hello... next invoke time is {0}", NextProcessing);
            return (true, default);
        }

        protected override void Cleanup() => throw new NotImplementedException();
    }
}
