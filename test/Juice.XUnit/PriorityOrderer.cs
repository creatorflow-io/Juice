using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit.v3;

namespace Juice.XUnit
{
    public class PriorityOrderer : ITestCaseOrderer
    {
        IReadOnlyCollection<TTestCase> ITestCaseOrderer.OrderTestCases<TTestCase>(
            IReadOnlyCollection<TTestCase> testCases)
        {
            var sortedMethods = new SortedDictionary<int, List<TTestCase>>();
            foreach (TTestCase testCase in testCases)
            {
                int priority = 0;
                if (testCase is IXunitTestCase xunitTestCase)
                {
                    priority = xunitTestCase.TestMethod.Method
                        .GetCustomAttribute<TestPriorityAttribute>()?.Priority ?? 0;
                }
                GetOrCreate(sortedMethods, priority).Add(testCase);
            }

            return sortedMethods.Keys
                .OrderByDescending(p => p)
                .SelectMany(priority => sortedMethods[priority]
                    .OrderBy(testCase => (testCase as IXunitTestCase)?.TestMethod.Method.Name ?? string.Empty))
                .ToList();
        }

        private static TValue GetOrCreate<TKey, TValue>(
            IDictionary<TKey, TValue> dictionary, TKey key)
            where TKey : struct
            where TValue : new() =>
            dictionary.TryGetValue(key, out TValue? result)
                ? result
                : (dictionary[key] = new TValue());
    }
}
