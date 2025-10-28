using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Juice.EventBus.Tests
{
    public class RoutingKeyUtilsTest
    {
        private ITestOutputHelper _output;

        public RoutingKeyUtilsTest(ITestOutputHelper output)
        {
            _output = output;
        }


        [Fact]
        public void Topic_should_match()
        {

            var key = ToMatchKey("kernel.*");
            _output.WriteLine(key);
            RoutingKeyUtils.IsTopicMatch("kernel.info", key).Should().BeTrue();
            RoutingKeyUtils.IsTopicMatch("kernel.info.x", key).Should().BeFalse();
            RoutingKeyUtils.IsTopicMatch("kernel", key).Should().BeFalse();
            RoutingKeyUtils.IsTopicMatch("x.kernel.info", key).Should().BeFalse();

            key = ToMatchKey("kernel.*.#");
            _output.WriteLine(key);
            RoutingKeyUtils.IsTopicMatch("kernel.info", key).Should().BeTrue();
            RoutingKeyUtils.IsTopicMatch("kernel.info.x", key).Should().BeTrue();
            RoutingKeyUtils.IsTopicMatch("kernel", key).Should().BeFalse();
            RoutingKeyUtils.IsTopicMatch("x.kernel.info", key).Should().BeFalse();

            key = ToMatchKey("kernel.*.*");
            _output.WriteLine(key);
            RoutingKeyUtils.IsTopicMatch("kernel.info", key).Should().BeFalse();
            RoutingKeyUtils.IsTopicMatch("kernel.info.x", key).Should().BeTrue();
            RoutingKeyUtils.IsTopicMatch("kernel.info.x.y", key).Should().BeFalse();
            RoutingKeyUtils.IsTopicMatch("kernel", key).Should().BeFalse();
            RoutingKeyUtils.IsTopicMatch("x.kernel.info", key).Should().BeFalse();

            key = ToMatchKey("*.kernel.*");
            _output.WriteLine(key);
            RoutingKeyUtils.IsTopicMatch("x.kernel.info", key).Should().BeTrue();
            RoutingKeyUtils.IsTopicMatch("kernel.info", key).Should().BeFalse();
            RoutingKeyUtils.IsTopicMatch("kernel.info.x", key).Should().BeFalse();

            key = ToMatchKey("#.kernel.*");
            _output.WriteLine(key);
            RoutingKeyUtils.IsTopicMatch("x.kernel.info", key).Should().BeTrue();
            RoutingKeyUtils.IsTopicMatch("kernel.info", key).Should().BeTrue();
            RoutingKeyUtils.IsTopicMatch("kernel.info.x", key).Should().BeFalse();

            key = ToMatchKey("kernel.#.info.*");
            _output.WriteLine(key);
            RoutingKeyUtils.IsTopicMatch("x.kernel.info", key).Should().BeFalse();
            RoutingKeyUtils.IsTopicMatch("kernel.info", key).Should().BeFalse();
            RoutingKeyUtils.IsTopicMatch("kernel.x.info.y", key).Should().BeTrue();
            RoutingKeyUtils.IsTopicMatch("kernel.x.y.info.z", key).Should().BeTrue();
            RoutingKeyUtils.IsTopicMatch("kernel.info.x", key).Should().BeTrue();
            RoutingKeyUtils.IsTopicMatch("kernel.info.x.y", key).Should().BeFalse();

            _output.WriteLine(RoutingKeyUtils.ToRouteMatchingKey("job.#.pending"));
            RoutingKeyUtils.IsTopicMatch("job.media.task.transcode.completed", "job.media.task.#").Should().BeTrue();
            RoutingKeyUtils.IsTopicMatch("job.media.task.transcode.completed", "job.media.task.#.pending").Should().BeFalse();
            RoutingKeyUtils.IsTopicMatch("job.media.task.transcode.pending", "job.media.task.#").Should().BeTrue();
            RoutingKeyUtils.IsTopicMatch("job.media.task.transcode.pending", "job.media.task.#.pending").Should().BeTrue();

            RoutingKeyUtils.IsTopicMatch("a.b", "*.*").Should().BeTrue();
        }
        private string ToMatchKey(string key)
        {
            return key;
        }
    }
}
