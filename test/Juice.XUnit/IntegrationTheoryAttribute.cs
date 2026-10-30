using System;
using Xunit;

namespace Juice.XUnit
{
    /// <summary>
    /// A theory that needs the test infrastructure (brokers, databases). It runs locally; on CI it is skipped unless
    /// the workflow provides that infrastructure and sets <c>JUICE_INTEGRATION_TESTS=true</c>.
    /// </summary>
    public class IntegrationTheoryAttribute : TheoryAttribute
    {
        public IntegrationTheoryAttribute()
        {
            if ("true".Equals(Environment.GetEnvironmentVariable("CI"))
                && !"true".Equals(Environment.GetEnvironmentVariable("JUICE_INTEGRATION_TESTS")))
            {
                Skip = "Needs the integration test infrastructure (JUICE_INTEGRATION_TESTS=true)";
            }
        }
    }
}
