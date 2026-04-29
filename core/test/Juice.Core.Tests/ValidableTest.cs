
using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using Xunit;

namespace Juice.Core.Tests
{
    public class ValidableTest
    {
        private readonly ITestOutputHelper _output;

        public ValidableTest(ITestOutputHelper output)
        {
            _output = output;
        }
        [Fact]
        public void Validate_result_should()
        {
            string? value = null;
            int number = -1;
            var rs = Validator.New.NotNullOrWhiteSpace(value)
                .NotNegative(number).ValidationResult();
            rs.Succeeded.Should().BeFalse();
            rs.Message.Should().Contain("Argument \"value\"");
            rs.Message.Should().Contain("Argument \"number\"");
            _output.WriteLine(rs.Message);
            _output.WriteLine(rs.StackTrace ?? "");

            var tg = new TG();
            rs = tg.Action();
            rs.Succeeded.Should().BeFalse();
            rs.Message.Should().Contain("Property \"Name\"");
            _output.WriteLine(rs.Message);
            _output.WriteLine(rs.StackTrace ?? "");

        }

    }

    internal class TG : IValidatable
    {
        public IList<string> ValidationErrors { get; } = [];

        public string? Name { get; set; }
        public string? Description { get; set; } = null;
        public IOperationResult Action()
        {
            return this.NotNullOrWhiteSpace(Name)
                .NotExceededLength(Description, 100)
                .ValidationResult();
        }
    }
}
