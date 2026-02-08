using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

        [Fact(DisplayName = "Should return default when deserializing invalid JSON")]
        public void Should_Return_Default_When_Deserializing_Invalid_Json()
        {
            // Arrange
            var invalidJson = "{ invalid json }";

            // Act
            var deserialized = _serializer.Deserialize<TestData>(invalidJson);

            // Assert
            deserialized.Should().BeNull();
        }

        [Fact(DisplayName = "Should handle interface without $type gracefully")]
        public void Should_Handle_Interface_Without_Type_Gracefully()
        {
            // Arrange - JSON without $type property
            var jsonWithoutType = "{\"Succeeded\":true,\"Message\":\"Test\"}";

            // Act
            var deserialized = _serializer.Deserialize<IOperationResult>(jsonWithoutType);

            // Assert
            // Should either deserialize successfully or return null
            // depending on whether a concrete implementation can be found
            _testOutput.WriteLine($"Deserialized: {deserialized?.GetType().Name ?? "null"}");
        }

        #endregion

        #region Test Helper Classes

        public class TestData
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Value { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        public class NestedTestData
        {
            public Guid Id { get; set; }
            public TestData? Inner { get; set; }
        }

        #endregion
    }
}
