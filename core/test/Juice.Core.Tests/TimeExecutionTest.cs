
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.Measurement.Internal;
using Xunit;
using Xunit.Abstractions;

namespace Juice.Core.Tests
{
    public class TimeExecutionTest
    {
        private readonly ITestOutputHelper _output;

        public TimeExecutionTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task TimeTracker_shouldAsync()
        {
            var timeTracker = new TimeTracker();
            timeTracker.Checkpoint("Start");
            var scopeId = "xunit.timetracker.scopeId";
            using (timeTracker.BeginScope("Test", scopeId))
            {
                // Do something
                timeTracker.Checkpoint("Checkpoint 0.0");
                await Task.Delay(12);
                timeTracker.Checkpoint("Checkpoint 0.1");

                using (timeTracker.BeginScope("Inner Test"))
                {
                    // Do something
                    timeTracker.Checkpoint("Checkpoint 1.0");
                    await Task.Delay(15);

                    using (timeTracker.BeginScope("Inner Inner Test"))
                    {
                        // Do something
                        timeTracker.Checkpoint("Checkpoint 1.1");
                        await Task.Delay(20);
                        timeTracker.Checkpoint("Checkpoint 1.2");
                    }
                }
                timeTracker.Checkpoint("Checkpoint 1");
                using (timeTracker.BeginScope("Inner Test 2"))
                {
                    // Do something
                    timeTracker.Checkpoint("Checkpoint 1.3.0");
                    await Task.Delay(10);
                    timeTracker.Checkpoint("Checkpoint 1.3.1");
                    timeTracker.Checkpoint("Checkpoint 1.4");
                }
            }
            timeTracker.Checkpoint("End");

            using var timeTracker2 = new TimeTracker();
            using var _ = timeTracker2.BeginScope("Test 2");
            _output.WriteLine(timeTracker.ToString());
            timeTracker2.Checkpoint("ToString");
            _output.WriteLine(timeTracker.ToString(false, 2));
            timeTracker2.Checkpoint("ToString 2");
            _output.WriteLine(timeTracker.ToString(true, 2, false));
            timeTracker2.Checkpoint("ToString 3");
            _.Dispose();
            _output.WriteLine(timeTracker2.ToString());
            timeTracker.Records.Should().Contain(c => c.Name == "Start");
            timeTracker.Records.Should().Contain(c => c.Name == "End");
            timeTracker.Records.Should().Contain(c => c.Name == "Checkpoint 0.0");
            timeTracker.Records.Should().Contain(c => c.Name == "Checkpoint 1.0");
            timeTracker.Records.Should().Contain(c => c.Name == "Checkpoint 1.1");
            timeTracker.Records.Should().Contain(c => c.Name == "Checkpoint 1.2");
            timeTracker.Records.Should().Contain(c => c.Name == "Checkpoint 1.3.1");
            timeTracker.Records.Should().Contain(c => c.Name == "Checkpoint 1.4");

            timeTracker.Records.Should().Contain(r => r.Name == "Test");
            timeTracker.Records.Should().Contain(r => r.Name == "Inner Test");
            timeTracker.Records.Should().Contain(r => r.Name == "Inner Inner Test");
            timeTracker.Records.Should().Contain(r => r.Name == "Inner Test 2");
            timeTracker.Records.OfType<IScope>().Any(s => s.ScopeId == scopeId).Should().BeTrue();
        }
    }
}
