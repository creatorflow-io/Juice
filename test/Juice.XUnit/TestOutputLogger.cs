using Juice.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Configuration;
using Newtonsoft.Json;
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
            var scopes = new List<object?>();
            scopeProvider?.ForEachScope((value, loggingProps) =>
            {
                scopes.Add(value);
            },
            entry.State);
            var scope = scopes.Any() ? JsonConvert.SerializeObject(scopes) : "";
            _output.WriteLine($"[{entry.LogLevel}]  {scope}   {entry.Category} - {formattedMessage}");
        }

        public override void ScopeDisposed<TState>(TState state)
        {
            _output.WriteLine($"ScopeDisposed: {state}");
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
