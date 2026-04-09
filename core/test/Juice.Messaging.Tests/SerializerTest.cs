using System;
using System.Text;
using FluentAssertions;
using Juice.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit.Abstractions;

namespace Juice.Messaging.Tests
{
    public class SerializerTest
    {
        private readonly ITestOutputHelper _testOutput;
        private readonly IMessageSerializer _serializer;

        public SerializerTest(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddTestOutputLogger(testOutput));
            services.AddMessaging();
            var provider = services.BuildServiceProvider();
            _serializer = provider.GetRequiredService<IMessageSerializer>();
        }

        #region String Type Tests

        [Fact(DisplayName = "Should serialize and deserialize string")]
        public void Should_Serialize_And_Deserialize_String()
        {
            // Arrange
            var original = "Hello, World!";

            // Act
            var serialized = _serializer.Serialize(original);
            var deserialized = _serializer.Deserialize<string>(serialized);

            // Assert
            _testOutput.WriteLine($"Original: {original}");
            _testOutput.WriteLine($"Serialized: {serialized}");
            _testOutput.WriteLine($"Deserialized: {deserialized}");

            deserialized.Should().Be(original);
        }

        [Fact(DisplayName = "Should handle null string")]
        public void Should_Handle_Null_String()
        {
            // Act
            var serialized = _serializer.Serialize((string?)null);
            var deserialized = _serializer.Deserialize<string>(serialized);

            // Assert
            serialized.Should().Be("null");
            deserialized.Should().BeNull();
        }

        #endregion

        #region Value Type Tests

        [Fact(DisplayName = "Should serialize and deserialize int")]
        public void Should_Serialize_And_Deserialize_Int()
        {
            // Arrange
            var original = 42;

            // Act
            var serialized = _serializer.Serialize(original);
            var deserialized = _serializer.Deserialize<int>(serialized);

            // Assert
            _testOutput.WriteLine($"Original: {original}");
            _testOutput.WriteLine($"Serialized: {serialized}");
            _testOutput.WriteLine($"Deserialized: {deserialized}");

            deserialized.Should().Be(original);
        }

        [Fact(DisplayName = "Should serialize and deserialize Guid")]
        public void Should_Serialize_And_Deserialize_Guid()
        {
            // Arrange
            var original = Guid.NewGuid();

            // Act
            var serialized = _serializer.Serialize(original);
            var deserialized = _serializer.Deserialize<Guid>(serialized);

            // Assert
            _testOutput.WriteLine($"Original: {original}");
            _testOutput.WriteLine($"Serialized: {serialized}");
            _testOutput.WriteLine($"Deserialized: {deserialized}");

            deserialized.Should().Be(original);
        }

        [Fact(DisplayName = "Should serialize and deserialize DateTime")]
        public void Should_Serialize_And_Deserialize_DateTime()
        {
            // Arrange
            var original = DateTime.UtcNow;

            // Act
            var serialized = _serializer.Serialize(original);
            var deserialized = _serializer.Deserialize<DateTime>(serialized);

            // Assert
            _testOutput.WriteLine($"Original: {original}");
            _testOutput.WriteLine($"Serialized: {serialized}");
            _testOutput.WriteLine($"Deserialized: {deserialized}");

            deserialized.Should().BeCloseTo(original, TimeSpan.FromMilliseconds(1));
        }

        #endregion

        #region Concrete Type Tests

        [Fact(DisplayName = "Should serialize and deserialize concrete class")]
        public void Should_Serialize_And_Deserialize_Concrete_Class()
        {
            // Arrange
            var original = new TestData
            {
                Id = Guid.NewGuid(),
                Name = "Test Name",
                Value = 100,
                CreatedAt = DateTime.UtcNow
            };

            // Act
            var serialized = _serializer.Serialize(original);
            var deserialized = _serializer.Deserialize<TestData>(serialized);

            // Assert
            _testOutput.WriteLine($"Serialized: {serialized}");
            
            deserialized.Should().NotBeNull();
            deserialized!.Id.Should().Be(original.Id);
            deserialized.Name.Should().Be(original.Name);
            deserialized.Value.Should().Be(original.Value);
            deserialized.CreatedAt.Should().BeCloseTo(original.CreatedAt, TimeSpan.FromMilliseconds(1));
        }

        #endregion

        #region Interface Type Tests

        [Fact(DisplayName = "Should serialize and deserialize bytes")]
        public void Should_Serialize_And_Deserialize_Bytes()
        {
            // Arrange
            IMessage original = new TestData
            {
                Id = Guid.NewGuid(),
                Name = "Interface Test",
                Value = 200,
                CreatedAt = DateTime.UtcNow
            };

            // Act
            byte[] SerializeToUtf8Bytes(object? value)
            {
                return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value);
            }
            var serialized = SerializeToUtf8Bytes(original);
            _testOutput.WriteLine("System.Text.Json JSON: {0}", Encoding.UTF8.GetString(serialized));
            
            var serialized1 = _serializer.SerializeToUtf8Bytes(original);
            _testOutput.WriteLine("System.Text.Json JSON: {0}", Encoding.UTF8.GetString(serialized1));

            var deserialized = _serializer.DeserializeFromUtf8Bytes<IMessage>(serialized, typeof(TestData));
            
            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.MessageId.Should().Be(original.MessageId);
            _testOutput.WriteLine("Deserialized: {0}", JsonConvert.SerializeObject(deserialized));

            var deserialized1 = _serializer.DeserializeFromUtf8Bytes<IMessage>(serialized1);
            _testOutput.WriteLine("Deserialized1: {0}", JsonConvert.SerializeObject(deserialized1));

            var json = Encoding.UTF8.GetString(serialized);
            _testOutput.WriteLine("JSON: {0}", json);
            var deserialized2 = _serializer.Deserialize<IMessage>(json, typeof(TestData));
            _testOutput.WriteLine("Deserialized2: {0}", JsonConvert.SerializeObject(deserialized2));

            Assert.Throws<JsonSerializationException>(() =>
            {
                var deserializedFail = _serializer.Deserialize<IMessage>(json);
            });

        }

        [Fact(DisplayName = "Should serialize and deserialize IOperationResult")]
        public void Should_Serialize_And_Deserialize_IOperationResult()
        {
            // Arrange
            var original = OperationResult.Succeeded("Operation completed successfully");

            // Act
            var serialized = _serializer.Serialize(original);
            _testOutput.WriteLine($"Serialized: {serialized}");

            var deserialized = _serializer.Deserialize<IOperationResult>(serialized);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Succeeded.Should().BeTrue();
            deserialized.Message.Should().Be("Operation completed successfully");
        }

        [Fact(DisplayName = "Should serialize and deserialize IOperationResult with failure")]
        public void Should_Serialize_And_Deserialize_IOperationResult_Failed()
        {
            // Arrange
            var original = OperationResult.Failed("Operation failed");

            // Act
            var serialized = _serializer.Serialize(original);
            _testOutput.WriteLine($"Serialized: {serialized}");

            var deserialized = _serializer.Deserialize<IOperationResult>(serialized);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Succeeded.Should().BeFalse();
            deserialized.Message.Should().Be("Operation failed");
        }

        [Fact(DisplayName = "Should serialize and deserialize IOperationResult<T>")]
        public void Should_Serialize_And_Deserialize_IOperationResult_Generic()
        {
            // Arrange
            var original = OperationResult.Result("Test Data", "Operation completed with data");

            // Act
            var serialized = _serializer.Serialize(original);
            _testOutput.WriteLine($"Serialized: {serialized}");

            var deserialized = _serializer.Deserialize<IOperationResult<string>>(serialized);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Succeeded.Should().BeTrue();
            deserialized.Data.Should().Be("Test Data");
            deserialized.Message.Should().Be("Operation completed with data");
        }

        [Fact(DisplayName = "Should serialize and deserialize IOperationResult<T> with complex data")]
        public void Should_Serialize_And_Deserialize_IOperationResult_Complex_Data()
        {
            // Arrange
            var testData = new TestData
            {
                Id = Guid.NewGuid(),
                Name = "Complex Test",
                Value = 999,
                CreatedAt = DateTime.UtcNow
            };
            var original = OperationResult.Result(testData, "Complex operation succeeded");

            // Act
            var serialized = _serializer.Serialize(original);
            _testOutput.WriteLine($"Serialized: {serialized}");

            var deserialized = _serializer.Deserialize<IOperationResult<TestData>>(serialized);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Succeeded.Should().BeTrue();
            deserialized.Data.Should().NotBeNull();
            deserialized.Data!.Id.Should().Be(testData.Id);
            deserialized.Data.Name.Should().Be(testData.Name);
            deserialized.Data.Value.Should().Be(testData.Value);
            deserialized.Message.Should().Be("Complex operation succeeded");
        }

        [Fact(DisplayName = "Should handle $type metadata for interfaces")]
        public void Should_Handle_Type_Metadata_For_Interfaces()
        {
            // Arrange
            var original = OperationResult.Result(123, "Success");

            // Act
            var serialized = _serializer.Serialize(original);
            _testOutput.WriteLine($"Serialized: {serialized}");

            // Assert - verify $type property exists in JSON
            serialized.Should().Contain("$type");
        }

        #endregion

        #region Collections Tests

        [Fact(DisplayName = "Should serialize and deserialize list")]
        public void Should_Serialize_And_Deserialize_List()
        {
            // Arrange
            var original = new List<TestData>
            {
                new TestData { Id = Guid.NewGuid(), Name = "Item 1", Value = 1 },
                new TestData { Id = Guid.NewGuid(), Name = "Item 2", Value = 2 },
                new TestData { Id = Guid.NewGuid(), Name = "Item 3", Value = 3 }
            };

            // Act
            var serialized = _serializer.Serialize(original);
            _testOutput.WriteLine($"Serialized: {serialized}");

            var deserialized = _serializer.Deserialize<List<TestData>>(serialized);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized.Should().HaveCount(3);
            deserialized![0].Name.Should().Be("Item 1");
            deserialized[1].Name.Should().Be("Item 2");
            deserialized[2].Name.Should().Be("Item 3");
        }

        #endregion

        #region Edge Cases

        [Fact(DisplayName = "Should handle null object")]
        public void Should_Handle_Null_Object()
        {
            // Act
            var serialized = _serializer.Serialize((object?)null);
            var deserialized = _serializer.Deserialize<object>(serialized);

            // Assert
            serialized.Should().Be("null");
            deserialized.Should().BeNull();
        }

        [Fact(DisplayName = "Should handle empty string deserialization")]
        public void Should_Handle_Empty_String_Deserialization()
        {
            // Act
            var deserialized = _serializer.Deserialize<string>(null);

            // Assert
            deserialized.Should().BeNull();
        }

        [Fact(DisplayName = "Should serialize object type")]
        public void Should_Serialize_Object_Type()
        {
            // Arrange
            var original = new { Name = "Anonymous", Value = 42 };

            // Act
            var serialized = _serializer.Serialize(original);
            _testOutput.WriteLine($"Serialized: {serialized}");

            // Assert
            serialized.Should().NotBeNullOrEmpty();
            serialized.Should().Contain("Name");
            serialized.Should().Contain("Anonymous");
        }

        [Fact(DisplayName = "Should handle nested objects")]
        public void Should_Handle_Nested_Objects()
        {
            // Arrange
            var original = new NestedTestData
            {
                Id = Guid.NewGuid(),
                Inner = new TestData
                {
                    Id = Guid.NewGuid(),
                    Name = "Inner Object",
                    Value = 50
                }
            };

            // Act
            var serialized = _serializer.Serialize(original);
            _testOutput.WriteLine($"Serialized: {serialized}");

            var deserialized = _serializer.Deserialize<NestedTestData>(serialized);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Id.Should().Be(original.Id);
            deserialized.Inner.Should().NotBeNull();
            deserialized.Inner!.Id.Should().Be(original.Inner.Id);
            deserialized.Inner.Name.Should().Be("Inner Object");
        }

        [Fact(DisplayName = "Should handle circular reference prevention")]
        public void Should_Handle_Type_Preservation()
        {
            // Arrange
            IOperationResult<int> original = OperationResult.Result(42);

            // Act
            var serialized = _serializer.Serialize(original);
            _testOutput.WriteLine($"Serialized: {serialized}");

            var deserialized = _serializer.Deserialize<IOperationResult<int>>(serialized);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Data.Should().Be(42);
        }

        #endregion

        #region Error Handling Tests

        [Fact(DisplayName = "Should throw when deserializing invalid JSON")]
        public void Should_Return_Default_When_Deserializing_Invalid_Json()
        {
            // Arrange
            var invalidJson = "{ invalid json }";

            // Act
            Assert.Throws<JsonReaderException>(() =>
            {
                var deserialized = _serializer.Deserialize<TestData>(invalidJson);
            });

        }

        #endregion

        #region IDictionary Tests

        [Fact(DisplayName = "Should serialize and deserialize class with IDictionary<string, object> property")]
        public void Should_Serialize_And_Deserialize_Class_With_Dictionary_Property()
        {
            // Arrange
            var guidValue = Guid.NewGuid();
            var original = new DictionaryData
            {
                Id = Guid.NewGuid(),
                Properties = new Dictionary<string, object?>
                {
                    ["name"]  = "Alice",
                    ["score"] = 99,
                    ["active"] = true,
                    ["guid"] = guidValue
                }
            };

            // Act
            var serialized = _serializer.Serialize(original);
            _testOutput.WriteLine($"Serialized: {serialized}");

            var deserialized = _serializer.Deserialize<IIntegrationEvent>(serialized, typeof(DictionaryData)) as DictionaryData;

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Id.Should().Be(original.Id);
            deserialized.Properties.Should().NotBeNull();
            deserialized.Properties!["name"].Should().Be("Alice");
            deserialized.Properties["score"].Should().Be(99L);   // JSON numbers → long when target is object
            deserialized.Properties["active"].Should().Be(true);
            deserialized.Properties["guid"].Should().Be(guidValue.ToString());
            deserialized.Properties.GetOption<Guid>("guid").Should().Be(guidValue);
        }

        [Fact(DisplayName = "Should round-trip string values in dictionary")]
        public void Should_Round_Trip_String_Values_In_Dictionary()
        {
            // Arrange
            var original = new DictionaryData
            {
                Properties = new Dictionary<string, object?>
                {
                    ["key1"] = "value1",
                    ["key2"] = "value2"
                }
            };

            // Act
            var serialized = _serializer.Serialize(original);
            var deserialized = _serializer.Deserialize<DictionaryData>(serialized);

            // Assert
            deserialized!.Properties!["key1"].Should().Be("value1");
            deserialized.Properties["key2"].Should().Be("value2");
        }

        [Fact(DisplayName = "Should handle null value in dictionary")]
        public void Should_Handle_Null_Value_In_Dictionary()
        {
            // Arrange
            var original = new DictionaryData
            {
                Properties = new Dictionary<string, object?>
                {
                    ["present"] = "yes",
                    ["absent"]  = null!
                }
            };

            // Act
            var serialized = _serializer.Serialize(original);
            _testOutput.WriteLine($"Serialized: {serialized}");

            var deserialized = _serializer.Deserialize<DictionaryData>(serialized);

            // Assert
            deserialized!.Properties!["present"].Should().Be("yes");
            deserialized.Properties["absent"].Should().BeNull();
        }

        [Fact(DisplayName = "Should handle empty dictionary property")]
        public void Should_Handle_Empty_Dictionary_Property()
        {
            // Arrange
            var original = new DictionaryData
            {
                Id = Guid.NewGuid(),
                Properties = new Dictionary<string, object?>()
            };

            // Act
            var serialized = _serializer.Serialize(original);
            _testOutput.WriteLine($"Serialized: {serialized}");

            var deserialized = _serializer.Deserialize<DictionaryData>(serialized);

            // Assert
            deserialized!.Properties.Should().NotBeNull();
            deserialized.Properties.Should().BeEmpty();
        }

        [Fact(DisplayName = "Dictionary nested object value deserializes as JObject")]
        public void Should_Deserialize_Nested_Object_In_Dictionary_As_JObject()
        {
            // Arrange - complex objects stored in object-typed dictionary slots
            // come back as JObject because plain Deserialize<T> (concrete) does not
            // apply TypeNameHandling, so $type metadata is not used.
            var original = new DictionaryData
            {
                Properties = new Dictionary<string, object?>
                {
                    ["inner"] = new TestData { Id = Guid.NewGuid(), Name = "nested", Value = 7 }
                }
            };

            // Act
            var serialized = _serializer.Serialize(original);
            _testOutput.WriteLine($"Serialized: {serialized}");

            var deserialized = _serializer.Deserialize<DictionaryData>(serialized);

            // Assert — value is present; consumer must cast to JObject / re-hydrate
            deserialized!.Properties!["inner"].Should().NotBeNull();
            deserialized.Properties["inner"].Should().BeOfType<Newtonsoft.Json.Linq.JObject>();
        }

        [Fact(DisplayName = "Should serialize and deserialize plain IDictionary<string,object> via eventType")]
        public void Should_Serialize_And_Deserialize_Plain_Dictionary_Via_EventType()
        {
            // Arrange
            var original = new Dictionary<string, object>
            {
                ["alpha"] = "hello",
                ["beta"]  = 42
            };

            // Act
            var serialized = _serializer.Serialize(original);
            _testOutput.WriteLine($"Serialized: {serialized}");

            // Pass concrete runtime type so deserializer avoids the interface path
            var deserialized = _serializer.Deserialize<IDictionary<string, object>>(
                serialized, typeof(Dictionary<string, object>));

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!["alpha"].Should().Be("hello");
            deserialized["beta"].Should().Be(42L);
        }

        #endregion

        #region Test Helper Classes

        public record TestData: MessageBase, IMessage
        {
            public Guid Id { get; set; }
            public string? Name { get; set; }
            public int Value { get; set; }
        }

        public class NestedTestData
        {
            public Guid Id { get; set; }
            public TestData? Inner { get; set; }
        }

        public record DictionaryData : IntegrationEvent
        {
            public Guid Id { get; set; }
            public IDictionary<string, object?>? Properties { get; set; }
        }

        #endregion
    }
}
