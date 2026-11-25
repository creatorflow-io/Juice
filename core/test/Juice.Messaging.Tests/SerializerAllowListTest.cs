using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit;

namespace Juice.Messaging.Tests
{
    public class SerializerAllowListTest
    {
        [Fact(DisplayName = "A type outside the allow-list fails with a warning that names the option")]
        public void Rejected_Type_Should_Log_Warning_Naming_The_Option()
        {
            var logs = new CapturingLoggerProvider();
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddProvider(logs));
            services.AddMessaging();
            var serializer = services.BuildServiceProvider().GetRequiredService<IMessageSerializer>();

            // The root type is allowed (Juice.*); a nested $type names an HD assembly the allow-list does not hold.
            var root = typeof(AllowListSample).AssemblyQualifiedName!.Split(", Version")[0];
            var payload = "{\"$type\":\"" + root + "\",\"Name\":\"x\"," +
                "\"Payload\":{\"$type\":\"HD.Station.Contents.ContentCreatedEvent, HD.Station.Contents\"}}";

            var act = () => serializer.Deserialize<IAllowListSample>(payload, null);

            act.Should().Throw<JsonSerializationException>();
            var warning = logs.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning).Subject;
            warning.Message.Should().Contain("HD.Station.Contents.ContentCreatedEvent")
                .And.Contain("AllowedAssemblyPrefixes");
            // The advice must not be built from the payload's assembly name.
            warning.Message.Should().NotContain("Add(\"HD.");
            logs.Entries.Should().NotContain(e => e.Level == LogLevel.Error);
        }

        [Fact(DisplayName = "A repeated rejection is logged at Debug, not dropped")]
        public void Repeated_Rejection_Should_Log_Debug()
        {
            var (serializer, logs) = Build();
            var payload = PayloadWithNestedType("HD.Station.Contents.ContentCreatedEvent, HD.Station.Contents");

            for (var i = 0; i < 3; i++)
            {
                var act = () => serializer.Deserialize<IAllowListSample>(payload, null);
                act.Should().Throw<JsonSerializationException>();
            }

            logs.Entries.Count(e => e.Level == LogLevel.Warning).Should().Be(1);
            logs.Entries.Count(e => e.Level == LogLevel.Debug && e.Message.Contains("ContentCreatedEvent")).Should().Be(2);
        }

        [Fact(DisplayName = "Warnings for distinct rejected names are capped")]
        public void Distinct_Rejections_Should_Be_Capped()
        {
            var (serializer, logs) = Build();

            for (var i = 0; i < 100; i++)
            {
                var act = () => serializer.Deserialize<IAllowListSample>(PayloadWithNestedType($"Evil.T{i}, Evil{i}"), null);
                act.Should().Throw<JsonSerializationException>();
            }

            logs.Entries.Count(e => e.Level == LogLevel.Warning).Should().Be(64);
            logs.Entries.Count(e => e.Level == LogLevel.Debug && e.Message.Contains("rejected")).Should().Be(36);
        }

        [Fact(DisplayName = "A $type without assembly gets advice that can work")]
        public void Rejected_Type_Without_Assembly_Should_Explain_Assembly_Name()
        {
            var (serializer, logs) = Build();

            var act = () => serializer.Deserialize<IAllowListSample>(PayloadWithNestedType("HD.Station.Contents.ContentCreatedEvent"), null);

            act.Should().Throw<JsonSerializationException>();
            logs.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning)
                .Which.Message.Should().Contain("no assembly name");
        }

        private static (IMessageSerializer Serializer, CapturingLoggerProvider Logs) Build()
        {
            var logs = new CapturingLoggerProvider();
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddProvider(logs).SetMinimumLevel(LogLevel.Debug));
            services.AddMessaging();
            return (services.BuildServiceProvider().GetRequiredService<IMessageSerializer>(), logs);
        }

        // The root type is allowed (Juice.*); the nested $type is the one under test.
        private static string PayloadWithNestedType(string nestedType)
        {
            var root = typeof(AllowListSample).AssemblyQualifiedName!.Split(", Version")[0];
            return "{\"$type\":\"" + root + "\",\"Name\":\"x\",\"Payload\":{\"$type\":\"" + nestedType + "\"}}";
        }

        [Fact(DisplayName = "A type whose prefix is allowed deserializes")]
        public void Allowed_Prefix_Should_Deserialize()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMessaging().ConfigureSerializer(o => o.AllowedAssemblyPrefixes.Add("Juice.Messaging.Tests"));
            var serializer = services.BuildServiceProvider().GetRequiredService<IMessageSerializer>();

            var payload = serializer.Serialize(new AllowListSample { Name = "x" });

            serializer.Deserialize<IAllowListSample>(payload, null)!.Name.Should().Be("x");
        }

        [Fact(DisplayName = "Framework collections of allowed types deserialize by $type, without a warning")]
        public void Framework_Collections_Of_Allowed_Types_Should_Deserialize()
        {
            var (serializer, logs) = Build();
            var sample = new CollectionSample
            {
                Name = "x",
                Entries = [new AllowListEntry { Key = "a" }],
                ByKey = new() { ["b"] = new AllowListEntry { Key = "b" } },
                Tags = ["t1", "t2"],
                Counts = [1, null],
                Ids = new HashSet<Guid> { Guid.Empty },
                Words = ["w"],
                Payload = new List<object> { new AllowListEntry { Key = "c" } }
            };

            var payload = serializer.Serialize(sample);
            var result = serializer.Deserialize<IAllowListSample>(payload, null);

            payload.Should().Contain("System.Collections.Generic.List`1[[");
            var collections = result.Should().BeOfType<CollectionSample>().Subject;
            collections.Entries.Should().ContainSingle().Which.Key.Should().Be("a");
            collections.ByKey["b"].Key.Should().Be("b");
            collections.Tags.Should().Equal("t1", "t2");
            collections.Counts.Should().Equal(1, null);
            collections.Ids.Should().Equal(Guid.Empty);
            collections.Words.Should().Equal("w");
            collections.Payload.Should().BeOfType<List<object>>().Which.Should().ContainSingle()
                .Which.Should().BeOfType<AllowListEntry>().Which.Key.Should().Be("c");
            logs.Entries.Should().NotContain(e => e.Level >= LogLevel.Warning);
        }

        [Theory(DisplayName = "A framework collection of a type outside the allow-list is rejected")]
        [InlineData("System.Collections.Generic.List`1[[HD.Station.Contents.ContentCreatedEvent, HD.Station.Contents]], System.Private.CoreLib")]
        [InlineData("System.Collections.Generic.Dictionary`2[[System.String, System.Private.CoreLib],[HD.Evil, HD.Evil]], System.Private.CoreLib")]
        [InlineData("System.Collections.Generic.List`1[[System.Collections.Generic.List`1[[HD.Evil, HD.Evil]], System.Private.CoreLib]], System.Private.CoreLib")]
        [InlineData("System.Collections.Generic.List`1[[System.Type, System.Private.CoreLib]], System.Private.CoreLib")]
        [InlineData("System.Collections.Generic.List`1[[System.String, System.Private.CoreLib]]x, System.Private.CoreLib")]
        [InlineData("System.Collections.Generic.Comparer`1[[System.String, System.Private.CoreLib]], System.Private.CoreLib")]
        [InlineData("System.Type, System.Private.CoreLib")]
        [InlineData("System.Type[], System.Private.CoreLib")]
        public void Framework_Collections_Of_Other_Types_Should_Be_Rejected(string type)
        {
            var (serializer, logs) = Build();

            var act = () => serializer.Deserialize<IAllowListSample>(PayloadWithNestedType(type), null);

            act.Should().Throw<JsonSerializationException>();
            logs.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning);
        }

        public class CollectionSample : AllowListSample
        {
            public List<AllowListEntry> Entries { get; set; } = [];
            public Dictionary<string, AllowListEntry> ByKey { get; set; } = [];
            public List<string> Tags { get; set; } = [];
            public List<int?> Counts { get; set; } = [];
            public HashSet<Guid> Ids { get; set; } = [];
            public string[] Words { get; set; } = [];
        }

        public class AllowListEntry
        {
            public string Key { get; set; } = "";
        }

        public interface IAllowListSample
        {
            string Name { get; }
        }

        public class AllowListSample : IAllowListSample
        {
            public string Name { get; set; } = "";
            public object? Payload { get; set; }
        }

        private sealed class CapturingLoggerProvider : ILoggerProvider
        {
            public ConcurrentQueue<(LogLevel Level, string Message)> Entries { get; } = new();

            public ILogger CreateLogger(string categoryName) => new Logger(this);

            public void Dispose() { }

            private sealed class Logger(CapturingLoggerProvider owner) : ILogger
            {
                public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
                public bool IsEnabled(LogLevel logLevel) => true;
                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                    Func<TState, Exception?, string> formatter)
                    => owner.Entries.Enqueue((logLevel, formatter(state, exception)));
            }
        }
    }
}
