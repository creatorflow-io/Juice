using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Juice.Core.Tests
{

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
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
            var builder = WebApplication.CreateBuilder();
            var services = builder.Services;
            var configuration = builder.Configuration;

            services.AddLogging(builder =>
            {
                builder.ClearProviders()
                .AddProvider(new TestOutputLoggerProvider(_output))
                .AddConfiguration(configuration.GetSection("Logging"));
            });

            var logger = services.BuildServiceProvider().GetRequiredService<ILogger<LoggerProviderTest>>();
            using (logger.BeginScope("Scope"))
            {
                logger.LogDebug("Log will not print");
                logger.LogInformation("Log ok");
                logger.LogWarning("Log ok");
            }
        }
    }
}
