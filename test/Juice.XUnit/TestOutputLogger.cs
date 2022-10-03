using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Configuration;
using Xunit.Abstractions;

namespace Microsoft.Extensions.Logging
{
    public class TestOutputLoggerOptions
    {
    }

    public class TestOutputLogger : ILogger
    {
        private readonly string _name;
        private readonly ITestOutputHelper _output;
        public TestOutputLogger(string name, ITestOutputHelper outputHelper) => (_name, _output) = (name, outputHelper);
        public IDisposable BeginScope<TState>(TState state) => default;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }
            if (exception != null)
            {
                _output.WriteLine($"[{logLevel}]     {_name} - {formatter(state, exception)}");
            }
            else
            {
                _output.WriteLine($"[{logLevel}]     {_name} - {state}");
            }
        }
    }

    public sealed class TestOutputLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentDictionary<string, TestOutputLogger> _loggers = new();
        private readonly IServiceProvider _serviceProvider;
        public TestOutputLoggerProvider(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ILogger CreateLogger(string categoryName) =>
            _loggers.GetOrAdd(categoryName, name => new TestOutputLogger(name, _serviceProvider.GetRequiredService<ITestOutputHelper>()));

        public void Dispose()
        {
            _loggers.Clear();
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
