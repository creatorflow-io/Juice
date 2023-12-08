using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.Extensions.DependencyInjection;
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


        [Fact(DisplayName = "Scopes should by logger")]
        public async Task LogScope_should_by_loggerAsync()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            var stringBuilder = new StringBuilder();
            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();
                services.AddSingleton(provider => _output);
                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                   .AddProvider(new TestOutputLoggerProvider(_output))
                   .AddProvider(new StringBuilderLoggerProvider(stringBuilder))
                   .AddConfiguration(configuration.GetSection("Logging"));
                });
            });

            var factory = resolver.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var logger1 = factory.CreateLogger("Logger1");
            var logger2 = factory.CreateLogger("Logger2");

            using (logger1.BeginScope("Scope1"))
            {
                logger1.LogInformation("Log1");
                var scope = stringBuilder.ToString().Trim();
                stringBuilder.Clear();
                _output.WriteLine(scope);
                scope.Should().StartWith(JsonConvert.SerializeObject(new string[] { "Scope1" }));

                logger2.LogInformation("Log2");
                scope = stringBuilder.ToString().Trim();
                stringBuilder.Clear();
                _output.WriteLine(scope);
                scope.Should().Be("Log2");

                using (logger2.BeginScope("Scope2"))
                {
                    logger1.LogInformation("Log1");
                    scope = stringBuilder.ToString().Trim();
                    stringBuilder.Clear();
                    _output.WriteLine(scope);
                    scope.Should().StartWith(JsonConvert.SerializeObject(new string[] { "Scope1" }));

                    using (logger1.BeginScope("Scope inside"))
                    {
                        logger1.LogInformation("Messsage inside nested scope");
                        scope = stringBuilder.ToString().Trim();
                        stringBuilder.Clear();
                        _output.WriteLine(scope);
                        scope.Should().StartWith(JsonConvert.SerializeObject(new string[] { "Scope1", "Scope inside" }));
                    }

                    logger2.LogInformation("Log2");
                    scope = stringBuilder.ToString().Trim();
                    stringBuilder.Clear();
                    _output.WriteLine(scope);
                    scope.Should().StartWith(JsonConvert.SerializeObject(new string[] { "Scope2" }));
                }
            }

            stringBuilder.Clear();
            using (logger1.BeginScope(new Dictionary<string, object>
            {
                ["Scope1"] = "Scope1",
                ["Scope2"] = "Scope2"
            }))
            {
                logger1.LogInformation("Log1");
                var scope = stringBuilder.ToString().Trim();
                stringBuilder.Clear();
                _output.WriteLine(scope);
                scope.Should().StartWith(JsonConvert.SerializeObject(new string[] { "Scope1", "Scope2" }));
            }
        }
    }

    internal class StringBuilderLoggerProvider : LoggerProvider
    {
        private readonly StringBuilder? _builder;
        public StringBuilderLoggerProvider(StringBuilder stringBuilder)
        {
            _builder = stringBuilder;
        }

        public override void WriteLog<TState>(LogEntry<TState> entry, string formattedMessage, IExternalScopeProvider? scopeProvider)
        {
            var scopes = new List<object?>();
            scopeProvider?.ForEachScope((value, loggingProps) =>
            {
                if (value is string s)
                {
                    scopes.Add(s);
                }
                else if (value is IEnumerable<KeyValuePair<string, object>> props)
                {
                    scopes.AddRange(props.Select(p => p.Key));
                }
            },
            entry.State);
            var scope = scopes.Any() ? JsonConvert.SerializeObject(scopes) : "";
            _builder?.Append(scope).Append(" ").Append(formattedMessage);
        }
    }
}
