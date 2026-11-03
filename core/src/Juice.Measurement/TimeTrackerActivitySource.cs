using System.Diagnostics;

namespace Juice.Measurement
{
    /// <summary>
    /// The <see cref="ActivitySource"/> <see cref="ITimeTracker"/> records its scopes on: each scope is a span and each
    /// checkpoint an event on it, so tracked steps appear in a trace's waterfall. A host collects them with
    /// <c>AddSource(TimeTrackerActivitySource.Name)</c>, which <c>AddJuiceInstrumentation()</c> includes. Without a
    /// listener no activity is created.
    /// </summary>
    public static class TimeTrackerActivitySource
    {
        public const string Name = "Juice.Measurement";

        /// <summary>The span tag holding a scope's <c>scopeId</c>.</summary>
        public const string ScopeIdTag = "juice.measurement.scope_id";

        public static readonly ActivitySource Source = new(Name);
    }
}
