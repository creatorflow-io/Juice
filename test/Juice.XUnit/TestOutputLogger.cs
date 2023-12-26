using Juice.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Configuration;
using Xunit.Abstractions;

namespace Microsoft.Extensions.Logging
{
    public class TestOutputLoggerOptions
    {
    }

    public sealed class TestOutputLoggerProvider : LoggerProvider
    {
        private readonly ITestOutputHelper _output;
        public TestOutputLoggerProvider(
            ITestOutputHelper testOutput)
        {
            _output = testOutput;
        }

        public override void WriteLog<TState>(LogEntry<TState> entry, string formattedMessage, IExternalScopeProvider? scopeProvider)
        {
            _output.WriteLine($"[{entry.LogLevel}] {entry.Category} - {formattedMessage}");
        }
        private List<string> _scopes = new();
        public override void ScopeStarted<TState>(string category, TState state, IExternalScopeProvider? scopeProvider)
        {
            _output.WriteLine(category);
            if (state is string scope)
            {
                var i = _scopes.IndexOf(scope);
                if (i == -1)
                {
                    var newScopes = new List<string>(_scopes)
                    {
                        scope
                    };
                    BeginScopes(_scopes, newScopes);
                    _scopes = newScopes;
                }
            }
            else if (state is string[] scopes)
            {
                var newScopes = new List<string>(_scopes);
                newScopes.AddRange(scopes);
                BeginScopes(_scopes, newScopes);
                _scopes = newScopes;
            }
            else if (state is IEnumerable<KeyValuePair<string, object>> kvps)
            {
                var objScopes = kvps.ToList();

                var newScopes = new List<string>(_scopes);
                newScopes.AddRange(objScopes.Select(s => $"{s.Key}: {s.Value}"));
                BeginScopes(_scopes, newScopes);
                _scopes = newScopes;

            }
        }

        private void BeginScopes(List<string> scopes, List<string> newScopes)
        {

            for (var i = 0; i < scopes.Count; i++)
            {
                if (scopes[i] != newScopes[i])
                {
                    for (var j = i; j < newScopes.Count; j++)
                    {
                        _output.WriteLine("{0} Begin: {1}", new string('-', (j + 1) * 4), newScopes[j]);
                    }
                    _output.WriteLine("");
                    return;
                }
            }
            if (newScopes.Count > scopes.Count)
            {
                for (var j = scopes.Count; j < newScopes.Count; j++)
                {
                    _output.WriteLine("{0} Begin: {1}", new string('-', (j + 1) * 4), newScopes[j]);
                }
                _output.WriteLine("");
            }
        }

        public override void ScopeDisposed<TState>(string category, TState state, IExternalScopeProvider? scopeProvider)
        {
            _output.WriteLine(category);
            if (state is string scope)
            {
                var i = _scopes.IndexOf(scope);
                if (i >= 0)
                {
                    var newScopes = _scopes.Take(i).ToList();
                    EndScopes(_scopes, newScopes);
                    _scopes = newScopes;
                }
            }
            else if (state is string[] scopes)
            {
                var newScopes = new List<string>(_scopes);
                foreach (var s in scopes)
                {
                    var i = newScopes.LastIndexOf(s);
                    if (i >= 0)
                    {
                        newScopes = newScopes.Take(i).ToList();
                    }
                }
                EndScopes(_scopes, newScopes);
                _scopes = newScopes;
            }
            else if (state is IEnumerable<KeyValuePair<string, object>> kvps)
            {

                var newScopes = new List<string>(_scopes);
                foreach (var s in kvps.Select(s => $"{s.Key}: {s.Value}"))
                {
                    var i = newScopes.LastIndexOf(s);
                    if (i >= 0)
                    {
                        newScopes = newScopes.Take(i).ToList();
                    }
                }
                EndScopes(_scopes, newScopes);
                _scopes = newScopes;
            }
        }

        private void EndScopes(List<string> scopes, List<string> newScopes)
        {
            for (var i = 0; i < scopes.Count; i++)
            {
                if (i >= newScopes.Count)
                {
                    _output.WriteLine("");
                    for (var j = scopes.Count - 1; j >= i; j--)
                    {
                        _output.WriteLine("{0}   End: {1}", new string('-', (j + 1) * 4), scopes[j]);
                        _output.WriteLine("");
                    }
                    break;
                }
                if (scopes[i] != newScopes[i])
                {
                    _output.WriteLine("");
                    for (var j = scopes.Count - 1; j >= i; j--)
                    {
                        _output.WriteLine("{0}   End: {1}", new string('-', (j + 1) * 4), scopes[j]);
                        _output.WriteLine("");
                    }
                    _output.WriteLine("");
                    return;
                }
            }
        }

    }

    public static class TestOutputLoggerExtensions
    {
        public static ILoggingBuilder AddTestOutputLogger(
            this ILoggingBuilder builder)
        {
            builder.AddConfiguration();

            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<ILoggerProvider, TestOutputLoggerProvider>());

            LoggerProviderOptions.RegisterProviderOptions
                <TestOutputLoggerOptions, TestOutputLoggerProvider>(builder.Services);

            return builder;
        }

        public static ILoggingBuilder AddTestOutputLogger(
            this ILoggingBuilder builder,
            Action<TestOutputLoggerOptions> configure)
        {
            builder.AddTestOutputLogger();
            builder.Services.Configure(configure);

            return builder;
        }
    }
}
