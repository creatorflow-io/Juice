using System;
using Xunit;

namespace Juice.XUnit
{
    public class IgnoreOnCITheoryAttribute : TheoryAttribute
    {
        public IgnoreOnCITheoryAttribute()
        {
            if ("true".Equals(Environment.GetEnvironmentVariable("CI")))
            {
                Skip = "Ignored on CI";
            }
        }
    }
}
