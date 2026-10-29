using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.Messaging;
using Juice.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Juice.Core.Tests
{
    public class MessageContextMiddlewareTest
    {
        [Fact]
        public async Task Finish_log_should_keep_the_message_context_Async()
        {
            var logs = new CapturingLoggerProvider();
            var httpContext = CreateHttpContext(logs);
            httpContext.Request.Headers["x-correlation-id"] = "corr-1";

            string? executionId = null;
            var middleware = new MessageContextMiddleware(_ =>
            {
                executionId = MessageContext.Current.ExecutionId;
                return Task.CompletedTask;
            }, "xunit");

            await middleware.InvokeAsync(httpContext);

            var finish = logs.States.Single(s => s.Contains("http.request.finish"));
            finish.Should().Contain("corr-1");
            finish.Should().Contain(executionId);
            finish.Should().Contain("xunit");
            MessageContext.IsInitialized.Should().BeFalse();
        }

        [Fact]
        public async Task Context_should_be_cleared_when_the_request_fails_Async()
        {
            var logs = new CapturingLoggerProvider();
            var httpContext = CreateHttpContext(logs);
            var middleware = new MessageContextMiddleware(_ => throw new InvalidOperationException("boom"), "xunit");

            await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(httpContext));

            logs.States.Single(s => s.Contains("http.request.finish")).Should().Contain("xunit");
            MessageContext.IsInitialized.Should().BeFalse();
        }

        private static DefaultHttpContext CreateHttpContext(CapturingLoggerProvider logs)
        {
            var services = new ServiceCollection()
                .AddLogging(builder => builder
                    .SetMinimumLevel(LogLevel.Debug)
                    .AddProvider(logs))
                .BuildServiceProvider();
            return new DefaultHttpContext { RequestServices = services };
        }

        private sealed class CapturingLoggerProvider : ILoggerProvider, ILogger
        {
            private readonly ConcurrentQueue<string> _states = new();
            public IReadOnlyList<string> States => _states.ToArray();

            public ILogger CreateLogger(string categoryName) => this;
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                // the {@ctx} argument is an anonymous object: capture its members, not just the formatted message
                var values = state is IEnumerable<KeyValuePair<string, object?>> pairs
                    ? string.Join(" ", pairs.Select(p => p.Value?.ToString()))
                    : "";
                _states.Enqueue(formatter(state, exception) + " " + values);
            }

            public void Dispose() { }
        }
    }
}
