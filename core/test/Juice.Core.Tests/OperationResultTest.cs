using System;
using FluentAssertions;
using Juice.Operation;
using Newtonsoft.Json;
using Xunit;
using Xunit;

namespace Juice.Core.Tests
{
    public class OperationResultTest
    {
        private ITestOutputHelper _output;

        public OperationResultTest(ITestOutputHelper testOutput)
        {
            _output = testOutput;
        }

        [Fact]
        public void OR_should_be_success()
        {
            var rs = OR.Success;
            rs.Succeeded.Should().BeTrue();
            _output.WriteLine(rs.ToString());

            var rs1 = OR.Succeeded("message");
            rs1.Succeeded.Should().BeTrue();
            rs1.Message.Should().Be("message");
            _output.WriteLine(rs1.ToString());
        }

        [Fact]
        public void OR_should_be_success_with_data()
        {
            var rs = OR.Result("data");
            rs.Succeeded.Should().BeTrue();
            rs.Data.Should().Be("data");
            rs.HasData.Should().BeTrue();
            rs.SucceededWithData.Should().BeTrue();
            _output.WriteLine(rs.ToString());
        }

        [Fact]
        public void OR_should_be_success_without_data()
        {
            var rs = OR.Succeeded<string>("message");
            rs.Succeeded.Should().BeTrue();
            rs.Data.Should().BeNull();
            rs.Message.Should().Be("message");
            rs.HasData.Should().BeFalse();
            rs.SucceededWithData.Should().BeFalse();
            _output.WriteLine(rs.ToString());
        }

        [Fact]
        public void OR_should_be_failed_with_data()
        {
            try
            {
                throw new Exception("Inner message");
            }catch (Exception ex)
            {
                var rs = OR.Failed(ex, "message", "string data");
                rs.Succeeded.Should().BeFalse();
                rs.Data.Should().Be("string data");
                rs.Message.Should().Be("message");
                rs.Exception.Should().NotBeNull();
                _output.WriteLine(rs.ToString());
                var _ = JsonConvert.SerializeObject(rs);
                _output.WriteLine(_);
                var _1 = System.Text.Json.JsonSerializer.Serialize(rs);
                _output.WriteLine(_1);
                _.Should().Be(_1);
            }
        }

        [Fact]
        public void OR_failed_should_be_corrected_params()
        {
            var rs = OR.Failed("message", new Exception("Inner message"));

            rs.Succeeded.Should().BeFalse();
            rs.Message.Should().Be("message");
            rs.Exception.Should().NotBeNull();
            _output.WriteLine(rs.ToString());
        }

        [Fact]
        public void OR_failed_should_has_exception_message()
        {
            var rs = OR.Failed(new Exception("Inner message"));

            rs.Succeeded.Should().BeFalse();
            rs.Message.Should().Be("Inner message");
            rs.Exception.Should().NotBeNull();
            _output.WriteLine(rs.ToString());
        }

        [Fact]
        public void OR_should_be_throwed_with_full_stack_trace()
        {
            var rs = Action();
            rs.StackTrace.Should().Contain("OperationResultTest.Action()");

            try
            {
                rs.ThrowIfNotSucceeded();
            }
            catch (Exception ex)
            {
                ex.Message.Should().Be("Inner message");
                _output.WriteLine(ex.StackTrace);
                ex.StackTrace.Should().Contain("OperationResultTest.Action()");
            }
        }

        [Fact]
        public void OR_should_be_changed_type()
        {
            var rs = OR.Failed(new Exception("Inner message")).Of<int>();
            rs.Succeeded.Should().BeFalse();
            rs.Exception.Should().NotBeNull();
            rs.Message.Should().Be("Inner message");
            rs.Data.Should().Be(0);
            _output.WriteLine(rs.ToString());
        }

        [Fact]
        public void OR_should_not_found()
        {
            var rs = OR.NotFound("message");
            rs.Succeeded.Should().BeFalse();
            rs.Failure.Should().Be(OperationalFailure.NotFound);
            _output.WriteLine(rs.ToString());
            var user = "test";
            var rs1 = OR.NotFound(user);
            _output.WriteLine(rs1.ToString());

            var rs2 = OR.NotFound<OR>(user);
            _output.WriteLine(rs2.ToString());

            var rs3 = rs2.Of<int?>("test. ");
            _output.WriteLine(rs3.ToString());
            rs3.Failure.Should().Be(OperationalFailure.NotFound);
            rs3.HasData.Should().BeFalse();

            rs3.IsNotFound().Should().BeTrue();

            Assert.Throws<InvalidOperationException>(() =>
            {
                _output.WriteLine(rs3.DataValue.ToString());
            });
        }

        [Fact]
        public void OR_should_unauthorized()
        {
            var rs = OR.Unauthorized("test");
            rs.Succeeded.Should().BeFalse();
            rs.Failure.Should().Be(OperationalFailure.Unauthorized);
            _output.WriteLine(rs.ToString());
            var user = "test";
            var rs1 = OR.Unauthorized(user);
            _output.WriteLine(rs1.ToString());

            var rs2 = OR.Unauthorized();
            _output.WriteLine(rs2.ToString());

            var rs3 = rs2.Of<int>();
            _output.WriteLine(rs3.ToString());
            rs3.Failure.Should().Be(OperationalFailure.Unauthorized);

            rs3.IsUnauthorized().Should().BeTrue();
        }

        [Fact]
        public void OR_should_not_implemented()
        {
            var rs = OR.NotImplemented("test");
            rs.Succeeded.Should().BeFalse();
            rs.Failure.Should().Be(OperationalFailure.NotImplemented);
            _output.WriteLine(rs.ToString());

            var rs2 = OR.NotImplemented<string>();
            _output.WriteLine(rs2.ToString());
            _output.WriteLine("---------------- rs2 ---------------");
            _output.WriteLine(rs2.Exception?.StackTrace ?? "");

            _output.WriteLine("------------------------------------");

            var rs3 = rs2.Of<int>();
            _output.WriteLine(rs3.ToString());
            rs3.Failure.Should().Be(OperationalFailure.NotImplemented);

            rs3.IsNotImplemented().Should().BeTrue();
            _output.WriteLine("---------------- rs3 ---------------");
            _output.WriteLine(rs3.Exception?.StackTrace ?? "");

            _output.WriteLine("------------------------------------");

            var rs4 = new TR().Action();
            rs4.IsNotImplemented().Should().BeTrue();
            _output.WriteLine(rs4.ToString());
            _output.WriteLine("---------------- rs4 ---------------");
            _output.WriteLine(rs4.Exception?.StackTrace ?? "");
            _output.WriteLine(rs4.StackTrace ?? "");
            _output.WriteLine("------------------------------------");
            Assert.Throws<OperationException>(() =>
            {
                rs4.ThrowIfNotSucceeded();
            });
            try { rs4.ThrowIfNotSucceeded(); }
            catch (Exception ex)
            {
                _output.WriteLine(ex.StackTrace);
            }
        }

        [Fact]
        public void OR_should_argument_null()
        {
            var rs = OR.ArgumentNull("test");
            rs.Succeeded.Should().BeFalse();
            rs.Failure.Should().Be(OperationalFailure.InvalidArgument);
            _output.WriteLine(rs.ToString());

            string? user = null;
            var rs1 = OR.ArgumentNull<int>(user);
            _output.WriteLine(rs1.ToString());

            var rs2 = OR.ArgumentNull(rs1);
            _output.WriteLine(rs2.ToString());

            var rs3 = rs2.Of<int>();
            _output.WriteLine(rs3.ToString());
            rs3.Failure.Should().Be(OperationalFailure.InvalidArgument);

            rs3.IsInvalidArgument().Should().BeTrue();
        }

        private IOperationResult Action()
        {
            try
            {
                throw new Exception("Inner message");
            }
            catch (Exception ex)
            {
                return OR.Failed(ex);
            }
        }

    }

    internal class TR
    {
        public IOperationResult Action()
        {
            return OR.NotImplemented();
        }
    }
    internal class OR : OperationResult
    {

    }

}
