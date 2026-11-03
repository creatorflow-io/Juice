using System.Diagnostics;

namespace Juice.Measurement.Internal
{
    /// <summary>
    /// Scoped service to track time. Each scope is also a span on <see cref="TimeTrackerActivitySource"/> and each
    /// checkpoint an event on the current scope's span.
    /// </summary>
	public class TimeTracker : ITimeTracker
    {
        private readonly ExecutionScope _rootScope = new("root", "", default);
        // The open scope of each async flow: parallel branches of a request (Task.WhenAll) nest their own scopes
        // instead of closing each other's, as Activity.Current does for spans.
        private readonly AsyncLocal<ExecutionScope?> _currentScope = new();

        /// <summary>
        /// Execution records. Lock on the list to read it while scopes may still be running.
        /// </summary>
        public List<ITrackRecord> Records { get; } = new();

        public ICollection<ScopeRecord> GetScopes()
        {
            var records = Snapshot();
            return records.OfType<IScope>()
                .Where(x => x.ScopeId != null).DistinctBy(x => x.ScopeId!)
                .Select(s =>
                {
                    var start = records.OfType<ScopeStart>().FirstOrDefault(x => x.ScopeId == s.ScopeId);
                    var end = records.OfType<ScopeEnd>().FirstOrDefault(x => x.ScopeId == s.ScopeId);
                    if (start == null || end == null)
                    {
                        return null;
                    }
                    return new ScopeRecord(start.Name, start.FullName, start.Depth, start.RecordTime, end.ElapsedTime, start.ScopeId!);
                }).OfType<ScopeRecord>().ToArray();
        }

        public TimeSpan ElapsedTime => _rootScope.ElapsedTime;

        /// <inheritdoc />
        public IDisposable BeginScope(string name, string? scopeId = default)
        {
            var parent = _currentScope.Value;
            var activity = TimeTrackerActivitySource.Source.StartActivity(name);
            if (scopeId != null)
            {
                activity?.SetTag(TimeTrackerActivitySource.ScopeIdTag, scopeId);
            }
            var scope = new ExecutionScope(name, parent == null ? name : $"{parent.FullName} > {name}", scopeId)
            {
                Activity = activity,
                Parent = parent,
                Depth = parent == null ? 0 : parent.Depth + 1,
            };
            scope.OnDispose += (sender, args) =>
            {
                Add(new ScopeEnd(scope.Name, scope.FullName, scope.Depth, _rootScope.ElapsedTime, scope.ElapsedTime, scope.ScopeId));
                _currentScope.Value = scope.Parent;
                if (scope.Parent == null)
                {
                    // Reset the checkpoint time.
                    _ = _rootScope.CheckpointTime;
                }
            };

            Add(new ScopeStart(name, scope.FullName, scope.Depth,
                _rootScope.ElapsedTime, parent?.ElapsedTime ?? _rootScope.CheckpointTime, scopeId));
            _currentScope.Value = scope;

            return scope;
        }

        /// <inheritdoc />
        public void Checkpoint(string name)
        {
            var current = _currentScope.Value;
            if (current != null)
            {
                Add(current.Checkpoint(name, current.Depth + 1, _rootScope.ElapsedTime));
                current.Activity?.AddEvent(new ActivityEvent(name));
            }
            else
            {
                Add(new Checkpoint(name, "", 0, _rootScope.ElapsedTime, _rootScope.CheckpointTime));
            }
        }

        private void Add(ITrackRecord record)
        {
            lock (Records)
            {
                Records.Add(record);
            }
        }

        private List<ITrackRecord> Snapshot()
        {
            lock (Records)
            {
                return Records.ToList();
            }
        }

        private static readonly string[] _header = new string[] { "Scope", "Depth", "Time Line", "Elapsed Time" };
        private static readonly ColAlign[] _colsAlign = new ColAlign[] { ColAlign.left, ColAlign.center, ColAlign.left, ColAlign.left };

        /// <inheritdoc />
        public string ToString(bool humanReadable, int? maxDepth = default, bool checkpoint = true)
        {
            // Create a table to display the execution records.
            var all = Snapshot();
            var records = all
                .Where(r => !maxDepth.HasValue || r.Depth <= maxDepth)
                .Where(r => checkpoint || r is not Internal.Checkpoint)
                .SelectMany(r => new string[][]
                    {
                        (new string[]{Name(r), r.Depth.ToString(), TimeLine(r, humanReadable), ElapsedTimeString(r, humanReadable) })
                    });

            if (records.Count() == 0)
            {
                return "No records.";
            }

            var table = new ConsoleTable(new string[][] { _header },
                records
                .Concat(new string[][] { new string[0], new string[] { "Total", "", "", ElapsedTimeToString(ElapsedTime, humanReadable) } }).ToArray());

            var nameMaxLength = all.Max(r => r.Name.Length + r.Depth + 2); // 2 for the prefix
            var timeMaxLength = records.Max(r => r[3].Length + 2);
            table.Cols = new int[] { nameMaxLength + 2, 10, humanReadable ? 15 : 20, timeMaxLength };
            table.ColsAlign = _colsAlign;
            return table.PrintTable();
        }

        private static string Name(ITrackRecord r)
        {
            return new string(' ', r.Depth) + r switch
            {
                ScopeStart => "« ",
                Checkpoint check => "› ",
                _ => "» "
            } + r.Name;
        }

        private static string TimeLine(ITrackRecord r, bool humanReadable) => ElapsedTimeToString(r.RecordTime, humanReadable, "› ", 3);
        private static string ElapsedTimeString(ITrackRecord r, bool humanReadable)
        {
            return r switch
            {
                Checkpoint check => new string(' ', r.Depth) + ElapsedTimeToString(check.ElapsedTime, humanReadable, "+ "),
                ScopeStart start => new string(' ', r.Depth) + ElapsedTimeToString(start.ElapsedTime, humanReadable, "+ "),
                ScopeEnd end => new string(' ', r.Depth) + ElapsedTimeToString(end.ElapsedTime, humanReadable),
                _ => ""
            };
        }
        private static string ElapsedTimeToString(TimeSpan elapsed, bool humanReadable, string prefix = "", int nums = 1)
        {
#if NET8_0_OR_GREATER
            return prefix + (humanReadable ? (elapsed.TotalMilliseconds >= 1 ? string.Format($"{{0:F{nums}}} ms", elapsed.TotalMilliseconds)
                : string.Format("{0} µs", elapsed.TotalMicroseconds)) : elapsed.ToString());
#else
            return prefix + (humanReadable ? (elapsed.TotalMilliseconds >= 1 ? string.Format($"{{0:F{nums}}} ms", elapsed.TotalMilliseconds)
                : string.Format("{0} ms", elapsed.TotalMilliseconds)) : elapsed.ToString(@"hh\:mm\:ss\.fff"));
#endif
        }

        override public string ToString()
        {
            return ToString(true);
        }


        private bool _disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    _currentScope.Value = null;
                    _rootScope.Dispose();
                    lock (Records)
                    {
                        Records.Clear();
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposedValue = true;
            }
        }
        void IDisposable.Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

}
