using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Juice.Messaging;
using Juice.Services;
using Xunit.v3;
using Xunit.Sdk;

namespace Juice.XUnit
{
    /// <summary>
    /// Custom xUnit attribute that automatically initializes MessageContext before test execution
    /// and clears it after test completion.
    /// </summary>
    /// <example>
    /// <code>
    /// [Fact]
    /// [InitializeMessageContext]
    /// public async Task MyTest_Should_Have_MessageContext()
    /// {
    ///     // MessageContext is already initialized
    ///     var correlationId = MessageContext.Current.CorrelationId;
    ///     // ... test logic
    /// }
    /// 
    /// // Custom correlation ID
    /// [Fact]
    /// [InitializeMessageContext(CorrelationId = "custom-correlation-123")]
    /// public async Task MyTest_With_Custom_CorrelationId()
    /// {
    ///     Assert.Equal("custom-correlation-123", MessageContext.Current.CorrelationId);
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class InitializeMessageContextAttribute : BeforeAfterTestAttribute
    {
        /// <summary>
        /// Optional: Custom correlation ID. If not set, a random GUID will be generated.
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Optional: Custom causation ID. If not set, null will be used.
        /// </summary>
        public string? CausationId { get; set; }

        /// <summary>
        /// Optional: Custom execution ID. If not set, a random GUID will be generated.
        /// </summary>
        public string? ExecutionId { get; set; }

        /// <summary>
        /// Optional: Custom source. Default is "xunit.test".
        /// </summary>
        public string Source { get; set; } = "xunit.test";

        /// <summary>
        /// Called before the test method is executed.
        /// </summary>
        public override void Before(MethodInfo methodUnderTest, IXunitTest test)
        {
            var correlationId = CorrelationId ?? StringIdGenerator.Instance.GenerateUniqueId();
            var executionId = ExecutionId ?? StringIdGenerator.Instance.GenerateUniqueId();

            MessageContext.Initialize(
                correlationId: correlationId,
                causationId: CausationId,
                executionId: executionId,
                source: Source
            );
        }

        /// <summary>
        /// Called after the test method is executed.
        /// </summary>
        public override void After(MethodInfo methodUnderTest, IXunitTest test)
        {
            MessageContext.Clear();
        }
    }
}
