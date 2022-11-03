using System.Collections.Generic;
using Juice.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Juice.Core.Tests
{

    public class TestLoggerProvider : LoggerProvider
    {
        private readonly ITestOutputHelper _output;
        public TestLoggerProvider(ITestOutputHelper testOutput)
        {
            _output = testOutput;
        }
        public override void WriteLog<TState>(LogEntry<TState> entry, string formattedMessage)
        {
            var scopes = new List<object?>();
            ScopeProvider.ForEachScope((value, loggingProps) =>
            {
                scopes.Add(value);
            },
            entry.State);
            _output.WriteLine($"Custom provider {entry.Category} scopes {JsonConvert.SerializeObject(scopes)} {formattedMessage}");
        }

        protected override void Cleanup() { }
    }

    public class LoggerProviderTest
    {
        private ITestOutputHelper _output;
        public LoggerProviderTest(ITestOutputHelper testOutput)
        {
            _output = testOutput;
        }
        [Fact(DisplayName = "Logging to test output")]
        public void LogTest()
        {
            var builder = WebApplication.CreateBuilder();
            var services = builder.Services;
            var configuration = builder.Configuration;

            services.AddLogging(builder =>
            {
                builder.ClearProviders()
                .AddProvider(new TestLoggerProvider(_output))
                .AddConfiguration(configuration.GetSection("Logging"));
            });

            var logger = services.BuildServiceProvider().GetRequiredService<ILogger<LoggerProviderTest>>();
            using (logger.BeginScope("Scope"))
            {
                logger.LogInformation("Log ok");
            }
        }
    }
}
