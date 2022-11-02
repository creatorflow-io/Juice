using System;
using Xunit;

namespace Juice.XUnit
{
    public class IgnoreOnCIFactAttribute : FactAttribute
    {
        public IgnoreOnCIFactAttribute()
        {
            if ("true".Equals(Environment.GetEnvironmentVariable("CI")))
            {
                Skip = "Ignored on CI";
            }
        }
    }
}
