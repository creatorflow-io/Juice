namespace Juice.Measurement.Internal
{
    /// <summary>
    /// Scoped service to track time.
    /// </summary>
	public class TimeTracker : ITimeTracker
    {
        private Stack<ExecutionScope> _scopes = new();
        private ExecutionScope _rootScope = new("root", "", default);
        private ExecutionScope? _currentScope;

        private Stack<string> _scopesName = new();
        public List<ITrackRecord> Records { get; } = new();
        public ICollection<ScopeRecord> GetScopes()
            => Records.OfType<IScope>()
                .Where(x => x.ScopeId != null).DistinctBy(x => x.ScopeId!)
                .Select(s =>
                {
                    var start = Records.OfType<ScopeStart>().FirstOrDefault(x => x.ScopeId == s.ScopeId);
                    var end = Records.OfType<ScopeEnd>().FirstOrDefault(x => x.ScopeId == s.ScopeId);
                    if (start == null || end == null)
                    {
                        return null;
                    }
                    return new ScopeRecord(start.Name, start.FullName, start.Depth, start.RecordTime, end.ElapsedTime, start.ScopeId!);
                }).OfType<ScopeRecord>().ToArray();

        public TimeSpan ElapsedTime => _rootScope.ElapsedTime;

        /// <inheritdoc />
        public IDisposable BeginScope(string name, string? scopeId = default)
        {
            _scopesName.Push(name);
            var scope = new ExecutionScope(name, GetScopeFullName(), scopeId);
            scope.OnDispose += (sender, args) =>
            {
                if (sender is ExecutionScope scope)
                {
                    Records.Add(new ScopeEnd(scope.Name, scope.FullName, _scopesName.Count - 1,
                        _rootScope.ElapsedTime, scope.ElapsedTime, scope.ScopeId));
                }
                _scopesName.Pop();
                _scopes.Pop();
                _currentScope = _scopes.Count > 0 ? _scopes.Peek() : null;
                if (_currentScope == null)
                {
                    // Reset the checkpoint time.
                    _ = _rootScope.CheckpointTime;
                }
            };
            _scopes.Push(scope);

            Records.Add(new ScopeStart(name, scope.FullName, _scopes.Count - 1,
                _rootScope.ElapsedTime, _currentScope?.ElapsedTime ?? _rootScope.CheckpointTime, scopeId));
            _currentScope = scope;

            return scope;
        }

        /// <inheritdoc />
        public void Checkpoint(string name)
        {
            if (_currentScope != null)
            {
                Records.Add(_currentScope.Checkpoint(name, _scopes.Count, _rootScope.ElapsedTime));
            }
            else
            {
                Records.Add(new Checkpoint(name, GetScopeFullName(), _scopes.Count, _rootScope.ElapsedTime, _rootScope.CheckpointTime));
            }
        }

        private static readonly string[] _header = new string[] { "Scope", "Depth", "Time Line", "Elapsed Time" };
        private static readonly ColAlign[] _colsAlign = new ColAlign[] { ColAlign.left, ColAlign.center, ColAlign.left, ColAlign.left };

        /// <inheritdoc />
        public string ToString(bool humanReadable, int? maxDepth = default, bool checkpoint = true)
        {
            // Create a table to display the execution records.
            var records = Records
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

            var nameMaxLength = Records.Max(r => r.Name.Length + r.Depth + 2); // 2 for the prefix
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

        private string GetScopeFullName()
        {
            return string.Join(" > ", _scopesName.Reverse());
        }

        private bool _disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    _scopesName.Clear();
                    _scopes.Clear();
                    _currentScope = null;
                    _rootScope.Dispose();
                    Records.Clear();
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
