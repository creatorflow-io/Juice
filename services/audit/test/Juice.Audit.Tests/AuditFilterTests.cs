using FluentAssertions;
using Juice.Audit.AspNetCore.Middleware;
using Xunit.Abstractions;

namespace Juice.Audit.Tests
{
    public class AuditFilterTests
    {
        private readonly ITestOutputHelper _output;

        public AuditFilterTests(ITestOutputHelper testOutput)
        {
            _output = testOutput;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }


        [Fact]
        public void Path_should_match()
        {
            var pattern = "kernel/*";
            _output.WriteLine(pattern);
            StringUtils.IsPathMatch("kernel/info", pattern).Should().BeTrue();
            StringUtils.IsPathMatch("kernel/info/x", pattern).Should().BeFalse();
            StringUtils.IsPathMatch("kernel", pattern).Should().BeFalse();
            StringUtils.IsPathMatch("x/kernel/info", pattern).Should().BeFalse();

            pattern = "kernel/*/#";
            _output.WriteLine(pattern);
            StringUtils.IsPathMatch("kernel/info", pattern).Should().BeTrue();
            StringUtils.IsPathMatch("kernel/info/x", pattern).Should().BeTrue();
            StringUtils.IsPathMatch("kernel/info/x/y/z", pattern).Should().BeTrue();
            StringUtils.IsPathMatch("kernel", pattern).Should().BeFalse();
            StringUtils.IsPathMatch("x/kernel/info", pattern).Should().BeFalse();

            pattern = "kernel/*/*";
            _output.WriteLine(pattern);
            StringUtils.IsPathMatch("kernel/info", pattern).Should().BeFalse();
            StringUtils.IsPathMatch("kernel/info/x", pattern).Should().BeTrue();
            StringUtils.IsPathMatch("kernel/info/x/y", pattern).Should().BeFalse();
            StringUtils.IsPathMatch("kernel", pattern).Should().BeFalse();
            StringUtils.IsPathMatch("x/kernel/info", pattern).Should().BeFalse();

            pattern = "*/kernel/*";
            _output.WriteLine(pattern);
            StringUtils.IsPathMatch("x/kernel/info", pattern).Should().BeTrue();
            StringUtils.IsPathMatch("kernel/info", pattern).Should().BeFalse();
            StringUtils.IsPathMatch("kernel/info/x", pattern).Should().BeFalse();

            pattern = "#/kernel/*";
            _output.WriteLine(pattern);
            StringUtils.IsPathMatch("x/kernel/info", pattern).Should().BeTrue();
            StringUtils.IsPathMatch("kernel/info", pattern).Should().BeTrue();
            StringUtils.IsPathMatch("kernel/info/x", pattern).Should().BeFalse();

            pattern = "/kernel/#/info/*";
            _output.WriteLine(pattern);
            StringUtils.IsPathMatch("/x/kernel/info", pattern).Should().BeFalse();
            StringUtils.IsPathMatch("/kernel/info", pattern).Should().BeFalse();
            StringUtils.IsPathMatch("/kernel/x/info/y", pattern).Should().BeTrue();
            StringUtils.IsPathMatch("/kernel/x/y/info/z", pattern).Should().BeTrue();
            StringUtils.IsPathMatch("/kernel/info/x", pattern).Should().BeTrue();
            StringUtils.IsPathMatch("/kernel/info/x/y", pattern).Should().BeFalse();

            pattern = "*";
            _output.WriteLine(pattern);
            StringUtils.IsPathMatch("x", pattern).Should().BeTrue();
            StringUtils.IsPathMatch("x/y", pattern).Should().BeFalse();
        }

        [Fact]
        public void Filter_should_match()
        {
            var filter = new AuditFilterOptions();
            filter.Include("", "POST", "PUT");
            filter.Include("kernel/*/#", "POST", "PUT", "PATCH");
            filter.Exclude("kernel/*/*", "POST");
            filter.Include("*/kernel/*", "GET");
            filter.Include("kernel/*/index");

            string? rule = default;
            filter.IsMatch("kernel/info", "GET", out rule).Should().BeFalse();
            rule.Should().BeNull();
            _output.WriteLine($"kernel/info does not matched. {rule ?? "none"}");

            filter.IsMatch("kernel/info", "POST", out rule).Should().BeTrue();
            rule.Should().BeSameAs("kernel/*/#");
            _output.WriteLine($"kernel/info matched. {rule ?? "none"}");

            filter.IsMatch("kernel/info/index", "GET", out rule).Should().BeTrue();
            rule.Should().BeSameAs("kernel/*/index");
            _output.WriteLine($"kernel/info/index matched. {rule ?? "none"}");

            filter.IsMatch("kernel/info/index", "POST", out rule).Should().BeTrue();
            rule.Should().BeSameAs("kernel/*/index");
            _output.WriteLine($"kernel/info/index matched. {rule ?? "none"}");

            filter.IsMatch("kernel/info/x", "PUT", out rule).Should().BeTrue();
            rule.Should().BeSameAs("kernel/*/#");
            _output.WriteLine($"kernel/info/x matched. {rule ?? "none"}");

            filter.IsMatch("kernel/info/x", "POST", out rule).Should().BeFalse();
            rule.Should().BeSameAs("kernel/*/*");
            _output.WriteLine($"kernel/info/x does not matched. {rule ?? "none"}");

            filter.IsMatch("kernel/info/x/y/z", "PATCH", out rule).Should().BeTrue();
            rule.Should().BeSameAs("kernel/*/#");
            _output.WriteLine($"kernel/info/x/y/z matched. {rule ?? "none"}");

            filter.IsMatch("kernel", "POST", out rule).Should().BeTrue();
            rule.Should().BeSameAs("");
            _output.WriteLine($"kernel matched. {rule ?? "none"}");

            filter.IsMatch("ker/info/index", "POST", out rule).Should().BeTrue();
            rule.Should().BeSameAs("");
            _output.WriteLine($"ker/info/index matched. {rule ?? "none"}");

            filter.IsMatch("ker/kernel/index", "GET", out rule).Should().BeTrue();
            rule.Should().BeSameAs("*/kernel/*");
            _output.WriteLine($"ker/kernel/index matched. {rule ?? "none"}");
        }

        [Fact]
        public void Header_should_match()
        {
            StringUtils.IsHeaderMatch(":authority:", ":authority:").Should().BeTrue();
            StringUtils.IsHeaderMatch("referer", "referer-*").Should().BeFalse();

            StringUtils.IsHeaderMatch("content", "content-*").Should().BeFalse();
            StringUtils.IsHeaderMatch("content-length", "content-*").Should().BeTrue();
            StringUtils.IsHeaderMatch("content-type", "content-*").Should().BeTrue();
            StringUtils.IsHeaderMatch("content-type-x", "content-*").Should().BeFalse();

            StringUtils.IsHeaderMatch("accept", "accept-#").Should().BeTrue();
            StringUtils.IsHeaderMatch("accept-encoding", "accept-#").Should().BeTrue();
            StringUtils.IsHeaderMatch("accept-language", "accept-#").Should().BeTrue();
            StringUtils.IsHeaderMatch("accept-a-b-c", "accept-#").Should().BeTrue();

            var options = new AuditFilterOptions();

            options.IsReqHeaderMatch("referer").Should().BeTrue();
            options.IsReqHeaderMatch("referer-a").Should().BeFalse();
            options.IsReqHeaderMatch("content-type").Should().BeTrue();
            options.IsReqHeaderMatch("content-type-x").Should().BeFalse();
            options.IsReqHeaderMatch("accept").Should().BeTrue();
            options.IsReqHeaderMatch("accept-encoding").Should().BeTrue();
            options.IsReqHeaderMatch("accept-language").Should().BeTrue();
            options.IsReqHeaderMatch("accept-a-b-c").Should().BeTrue();
            options.IsReqHeaderMatch(":authority:").Should().BeTrue();
        }

    }
}
