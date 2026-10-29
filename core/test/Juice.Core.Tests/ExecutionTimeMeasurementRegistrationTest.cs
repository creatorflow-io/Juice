using FluentAssertions;
using Juice.Measurement;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Juice.Core.Tests
{
    public class ExecutionTimeMeasurementRegistrationTest
    {
        [Fact]
        public void Measurement_should_be_registered_by_default()
        {
            using var provider = new ServiceCollection().AddExecutionTimeMeasurement().BuildServiceProvider();
            using var scope = provider.CreateScope();

            scope.ServiceProvider.GetService<ITimeTracker>().Should().NotBeNull();
        }

        [Fact]
        public void Measurement_should_not_be_registered_when_disabled()
        {
            using var provider = new ServiceCollection().AddExecutionTimeMeasurement(enabled: false).BuildServiceProvider();
            using var scope = provider.CreateScope();

            scope.ServiceProvider.GetService<ITimeTracker>().Should().BeNull();
        }

        [Fact]
        public void Obsolete_callback_should_keep_registering_the_tracker()
        {
#pragma warning disable CS0618
            using var provider = new ServiceCollection().AddExecutionTimeMeasurement(enabled => { }).BuildServiceProvider();
#pragma warning restore CS0618
            using var scope = provider.CreateScope();

            scope.ServiceProvider.GetService<ITimeTracker>().Should().NotBeNull();
        }
    }
}
