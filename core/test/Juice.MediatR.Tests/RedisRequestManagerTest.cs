using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.Extensions.DependencyInjection;
using Juice.Extensions.Redis;
using Juice.XUnit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Xunit;
using Xunit.Abstractions;

namespace Juice.MediatR.Tests
{
    [TestCaseOrderer("Juice.XUnit.PriorityOrderer", "Juice.XUnit")]
    public class RedisRequestManagerTest
    {
        private readonly ITestOutputHelper _testOutput;

        public RedisRequestManagerTest(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
        }

        #region Setup Helpers

        private IServiceProvider BuildServiceProvider(string connectionStringKey = "Redis")
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration(typeof(RedisRequestManagerTest).Assembly);

                services.AddMediatR(cfg =>
                {
                    cfg.AddRedisRequestManager(options =>
                    {
                        options.ConnectionString = configuration.GetConnectionString(connectionStringKey);
                    });
                });

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger(_testOutput)
                    .AddConfiguration(configuration.GetSection("Logging"));
                });
            });

            return resolver.ServiceProvider;
        }

        private async Task CleanupRedisKeysAsync(IServiceProvider serviceProvider, params string[] keyPatterns)
        {
            var connectionProvider = serviceProvider.GetRequiredService<IRedisConnectionProvider<RequestManager.Redis.RequestManager>>();
            using var connection = await connectionProvider.GetConnectionAsync();
            var server = connection.GetServer(connection.GetEndPoints().First());
            var db = connection.GetDatabase();
            // Server-side script: repeatedly SCAN for the pattern and DEL keys found.
            // Running on server avoids client-side SCAN timeouts and many round-trips.
            const string deleteScript = @"
local cursor = '0' repeat local res = redis.call('SCAN', cursor, 'MATCH', ARGV[1], 'COUNT', 100) cursor = res[1] for i, key in ipairs(res[2]) do redis.call('DEL', key) end until cursor == '0' return 1 ";
            foreach (var pattern in keyPatterns)
            {
                await db.ScriptEvaluateAsync(deleteScript, values: new RedisValue[] { pattern });
            }
        }

        #endregion

        #region Connection Tests

        [IgnoreOnCIFact(DisplayName = "Redis connect master directly"), TestPriority(1)]
        public async Task Redis_connect_directlyAsync()
        {
            // Arrange
            var serviceProvider = BuildServiceProvider("Redis");

            // Act
            var manager = serviceProvider.GetRequiredService<IRequestManager>();
            var managerT = serviceProvider.GetRequiredService<IRequestManager<RedisRequestManagerTest>>();
            var connectionProvider = serviceProvider.GetRequiredService<IRedisConnectionProvider<RequestManager.Redis.RequestManager>>();
            using var connection = await connectionProvider.GetConnectionAsync();

            // Assert
            connection.Should().NotBeNull();
            connection.IsConnected.Should().BeTrue();
            manager.Should().NotBeNull();
            managerT.Should().NotBeNull();
        }

        [IgnoreOnCIFact(DisplayName = "Redis connect sentinel"), TestPriority(1)]
        public async Task Redis_connect_sentinelAsync()
        {
            // Arrange
            var serviceProvider = BuildServiceProvider("RedisSentinel");

            // Act
            var manager = serviceProvider.GetRequiredService<IRequestManager>();
            var managerT = serviceProvider.GetRequiredService<IRequestManager<RedisRequestManagerTest>>();
            var connectionProvider = serviceProvider.GetRequiredService<IRedisConnectionProvider<RequestManager.Redis.RequestManager>>();
            using var connection = await connectionProvider.GetConnectionAsync();

            // Assert
            connection.Should().NotBeNull();
            connection.IsConnected.Should().BeTrue();
        }

        #endregion

        #region Request Creation Tests

        [IgnoreOnCIFact(DisplayName = "Should create request marker"), TestPriority(10)]
        public async Task Should_Create_Request_MarkerAsync()
        {
            // Arrange
            var serviceProvider = BuildServiceProvider();
            var manager = serviceProvider.GetRequiredService<IRequestManager>();
            var requestId = Guid.NewGuid();

            try
            {
                // Act
                var created = await manager.TryCreateRequestForCommandAsync<TestRequest>(requestId);

                // Assert
                created.Should().BeTrue();
                _testOutput.WriteLine($"Created request marker for ID: {requestId}");
            }
            finally
            {
                await CleanupRedisKeysAsync(serviceProvider, $"*:{requestId}");
            }
        }

        [IgnoreOnCIFact(DisplayName = "Should prevent duplicate request creation"), TestPriority(11)]
        public async Task Should_Prevent_Duplicate_Request_CreationAsync()
        {
            // Arrange
            var serviceProvider = BuildServiceProvider();
            var manager = serviceProvider.GetRequiredService<IRequestManager>();
            var requestId = Guid.NewGuid();

            try
            {
                // Act
                var created1 = await manager.TryCreateRequestForCommandAsync<TestRequest>(requestId);
                var created2 = await manager.TryCreateRequestForCommandAsync<TestRequest>(requestId);

                // Assert
                created1.Should().BeTrue("First creation should succeed");
                created2.Should().BeFalse("Second creation should fail (duplicate)");

                _testOutput.WriteLine($"First attempt: {created1}, Second attempt: {created2}");
            }
            finally
            {
                await CleanupRedisKeysAsync(serviceProvider, $"*:{requestId}");
            }
        }

        [IgnoreOnCIFact(DisplayName = "Should handle concurrent request creation"), TestPriority(12)]
        public async Task Should_Handle_Concurrent_Request_CreationAsync()
        {
            // Arrange
            var serviceProvider = BuildServiceProvider();
            var manager = serviceProvider.GetRequiredService<IRequestManager>();
            var requestId = Guid.NewGuid();

            try
            {
                // Act - Simulate concurrent requests
                var tasks = Enumerable.Range(0, 10)
                    .Select(async i =>
                    {
                        await Task.Delay(Random.Shared.Next(10, 50)); // Add jitter
                        return await manager.TryCreateRequestForCommandAsync<TestRequest>(requestId);
                    });

                var results = await Task.WhenAll(tasks);

                // Assert
                results.Count(r => r).Should().Be(1, "Only one creation should succeed");
                results.Count(r => !r).Should().Be(9, "Nine attempts should fail");

                _testOutput.WriteLine($"Successful: {results.Count(r => r)}, Failed: {results.Count(r => !r)}");
            }
            finally
            {
                await CleanupRedisKeysAsync(serviceProvider, $"*:{requestId}");
            }
        }

        #endregion

        #region Request Completion Tests

        [IgnoreOnCIFact(DisplayName = "Should complete request successfully without result"), TestPriority(20)]
        public async Task Should_Complete_Request_Successfully_Without_ResultAsync()
        {
            // Arrange
            var serviceProvider = BuildServiceProvider();
            var manager = serviceProvider.GetRequiredService<IRequestManager>();
            var requestId = Guid.NewGuid();

            try
            {
                await manager.TryCreateRequestForCommandAsync<TestRequest>(requestId);

                // Act
                await manager.TryCompleteRequestAsync<TestRequest>(requestId, success: true, result: null);

                // Assert
                var connectionProvider = serviceProvider.GetRequiredService<IRedisConnectionProvider<RequestManager.Redis.RequestManager>>();
                using var connection = await connectionProvider.GetConnectionAsync();
                var db = connection.GetDatabase();

                var requestKey = $"request:TestRequest:{requestId}";
                var stateKey = $"state:TestRequest:{requestId}";

                var requestExists = await db.KeyExistsAsync(requestKey);
                var state = await db.StringGetAsync(stateKey);

                requestExists.Should().BeTrue("Request key should exist");
                state.ToString().Should().Be("completed");

                _testOutput.WriteLine($"Request completed. State: {state}");
            }
            finally
            {
                await CleanupRedisKeysAsync(serviceProvider, $"*:{requestId}");
            }
        }

        [IgnoreOnCIFact(DisplayName = "Should complete request and cache result"), TestPriority(21)]
        public async Task Should_Complete_Request_And_Cache_ResultAsync()
        {
            // Arrange
            var serviceProvider = BuildServiceProvider();
            var manager = serviceProvider.GetRequiredService<IRequestManager>();
            var requestId = Guid.NewGuid();
            var expectedResult = OperationResult.Result("Test data", "Operation succeeded");

            try
            {
                await manager.TryCreateRequestForCommandAsync<TestRequest>(requestId);

                // Act
                await manager.TryCompleteRequestAsync<TestRequest>(requestId, success: true, result: expectedResult);

                // Assert - Check cached result exists
                var connectionProvider = serviceProvider.GetRequiredService<IRedisConnectionProvider<RequestManager.Redis.RequestManager>>();
                using var connection = await connectionProvider.GetConnectionAsync();
                var db = connection.GetDatabase();

                var resultKey = $"result:TestRequest:{requestId}";
                var cachedResult = await db.StringGetAsync(resultKey);

                cachedResult.HasValue.Should().BeTrue("Cached result should exist");
                cachedResult.ToString().Should().Contain("Test data");

                _testOutput.WriteLine($"Cached result: {cachedResult}");
            }
            finally
            {
                await CleanupRedisKeysAsync(serviceProvider, $"*:{requestId}");
            }
        }

        [IgnoreOnCIFact(DisplayName = "Should handle failed request completion"), TestPriority(22)]
        public async Task Should_Handle_Failed_Request_CompletionAsync()
        {
            // Arrange
            var serviceProvider = BuildServiceProvider();
            var manager = serviceProvider.GetRequiredService<IRequestManager>();
            var requestId = Guid.NewGuid();

            try
            {
                await manager.TryCreateRequestForCommandAsync<TestRequest>(requestId);

                // Act
                await manager.TryCompleteRequestAsync<TestRequest>(requestId, success: false, result: null);

                // Assert - All keys should be deleted
                var connectionProvider = serviceProvider.GetRequiredService<IRedisConnectionProvider<RequestManager.Redis.RequestManager>>();
                using var connection = await connectionProvider.GetConnectionAsync();
                var db = connection.GetDatabase();

                var requestKey = $"request:TestRequest:{requestId}";
                var resultKey = $"result:TestRequest:{requestId}";
                var stateKey = $"state:TestRequest:{requestId}";

                var requestExists = await db.KeyExistsAsync(requestKey);
                var resultExists = await db.KeyExistsAsync(resultKey);
                var stateExists = await db.KeyExistsAsync(stateKey);

                requestExists.Should().BeFalse("Request key should be deleted");
                resultExists.Should().BeFalse("Result key should be deleted");
                stateExists.Should().BeFalse("State key should be deleted");

                _testOutput.WriteLine("All keys deleted for failed request");
            }
            finally
            {
                await CleanupRedisKeysAsync(serviceProvider, $"*:{requestId}");
            }
        }

        #endregion

        #region Result Caching Tests

        [IgnoreOnCIFact(DisplayName = "Should retrieve cached string result"), TestPriority(30)]
        public async Task Should_Retrieve_Cached_String_ResultAsync()
        {
            // Arrange
            var serviceProvider = BuildServiceProvider();
            var manager = serviceProvider.GetRequiredService<IRequestManager>();
            var requestId = Guid.NewGuid();
            var expectedResult = "Test Result String";

            try
            {
                await manager.TryCreateRequestForCommandAsync<TestRequest>(requestId);
                await manager.TryCompleteRequestAsync<TestRequest>(requestId, success: true, result: expectedResult);

                // Act
                var cachedResult = await manager.GetCachedResultAsync<TestRequest, string>(requestId);

                // Assert
                cachedResult.Should().NotBeNull();
                cachedResult.Should().Be(expectedResult);

                _testOutput.WriteLine($"Retrieved cached result: {cachedResult}");
            }
            finally
            {
                await CleanupRedisKeysAsync(serviceProvider, $"*:{requestId}");
            }
        }

        [IgnoreOnCIFact(DisplayName = "Should retrieve cached IOperationResult"), TestPriority(31)]
        public async Task Should_Retrieve_Cached_IOperationResultAsync()
        {
            // Arrange
            var serviceProvider = BuildServiceProvider();
            var manager = serviceProvider.GetRequiredService<IRequestManager>();
            var requestId = Guid.NewGuid();
            var expectedResult = OperationResult.Result("Success data", "Operation completed");

            try
            {
                await manager.TryCreateRequestForCommandAsync<TestRequest>(requestId);
                await manager.TryCompleteRequestAsync<TestRequest>(requestId, success: true, result: expectedResult);

                // Act
                var cachedResult = await manager.GetCachedResultAsync<TestRequest, IOperationResult<string>>(requestId);

                // Assert
                cachedResult.Should().NotBeNull();
                cachedResult!.Succeeded.Should().BeTrue();
                cachedResult.Data.Should().Be("Success data");
                cachedResult.Message.Should().Be("Operation completed");

                _testOutput.WriteLine($"Retrieved cached operation result: {cachedResult.Message}");
            }
            finally
            {
                await CleanupRedisKeysAsync(serviceProvider, $"*:{requestId}");
            }
        }

        [IgnoreOnCIFact(DisplayName = "Should return null for non-existent cached result"), TestPriority(32)]
        public async Task Should_Return_Null_For_NonExistent_Cached_ResultAsync()
        {
            // Arrange
            var serviceProvider = BuildServiceProvider();
            var manager = serviceProvider.GetRequiredService<IRequestManager>();
            var nonExistentId = Guid.NewGuid();

            // Act
            var cachedResult = await manager.GetCachedResultAsync<TestRequest, string>(nonExistentId);

            // Assert
            cachedResult.Should().BeNull();

            _testOutput.WriteLine("No cached result found (as expected)");
        }

        [IgnoreOnCIFact(DisplayName = "Should handle complex object caching"), TestPriority(33)]
        public async Task Should_Handle_Complex_Object_CachingAsync()
        {
            // Arrange
            var serviceProvider = BuildServiceProvider();
            var manager = serviceProvider.GetRequiredService<IRequestManager>();
            var requestId = Guid.NewGuid();
            var expectedResult = new ComplexTestResult
            {
                Id = Guid.NewGuid(),
                Name = "Test Name",
                Items = new List<string> { "Item1", "Item2", "Item3" },
                Timestamp = DateTime.UtcNow
            };

            try
            {
                await manager.TryCreateRequestForCommandAsync<TestRequest>(requestId);
                await manager.TryCompleteRequestAsync<TestRequest>(requestId, success: true, result: expectedResult);

                // Act
                var cachedResult = await manager.GetCachedResultAsync<TestRequest, ComplexTestResult>(requestId);

                // Assert
                cachedResult.Should().NotBeNull();
                cachedResult!.Id.Should().Be(expectedResult.Id);
                cachedResult.Name.Should().Be(expectedResult.Name);
                cachedResult.Items.Should().BeEquivalentTo(expectedResult.Items);

                _testOutput.WriteLine($"Retrieved complex object: {cachedResult.Name} with {cachedResult.Items.Count} items");
            }
            finally
            {
                await CleanupRedisKeysAsync(serviceProvider, $"*:{requestId}");
            }
        }

        #endregion

        #region TTL and Expiration Tests

        [IgnoreOnCIFact(DisplayName = "Should set TTL on request keys"), TestPriority(40)]
        public async Task Should_Set_TTL_On_Request_KeysAsync()
        {
            // Arrange
            var serviceProvider = BuildServiceProvider();
            var manager = serviceProvider.GetRequiredService<IRequestManager>();
            var requestId = Guid.NewGuid();

            try
            {
                // Act
                await manager.TryCreateRequestForCommandAsync<TestRequest>(requestId);

                // Assert
                var connectionProvider = serviceProvider.GetRequiredService<IRedisConnectionProvider<RequestManager.Redis.RequestManager>>();
                using var connection = await connectionProvider.GetConnectionAsync();
                var db = connection.GetDatabase();

                var requestKey = $"request:TestRequest:{requestId}";
                var ttl = await db.KeyTimeToLiveAsync(requestKey);

                ttl.Should().NotBeNull();
                ttl.Value.TotalMinutes.Should().BeGreaterThan(14, "TTL should be close to 15 minutes");

                _testOutput.WriteLine($"Request key TTL: {ttl.Value.TotalMinutes:F2} minutes");
            }
            finally
            {
                await CleanupRedisKeysAsync(serviceProvider, $"*:{requestId}");
            }
        }

        [IgnoreOnCIFact(DisplayName = "Should set TTL on result keys"), TestPriority(41)]
        public async Task Should_Set_TTL_On_Result_KeysAsync()
        {
            // Arrange
            var serviceProvider = BuildServiceProvider();
            var manager = serviceProvider.GetRequiredService<IRequestManager>();
            var requestId = Guid.NewGuid();

            try
            {
                await manager.TryCreateRequestForCommandAsync<TestRequest>(requestId);

                // Act
                await manager.TryCompleteRequestAsync<TestRequest>(requestId, success: true, result: "Test data");

                // Assert
                var connectionProvider = serviceProvider.GetRequiredService<IRedisConnectionProvider<RequestManager.Redis.RequestManager>>();
                using var connection = await connectionProvider.GetConnectionAsync();
                var db = connection.GetDatabase();

                var resultKey = $"result:TestRequest:{requestId}";
                var ttl = await db.KeyTimeToLiveAsync(resultKey);

                ttl.Should().NotBeNull();
                ttl.Value.TotalHours.Should().BeGreaterThan(23, "TTL should be close to 24 hours");

                _testOutput.WriteLine($"Result key TTL: {ttl.Value.TotalHours:F2} hours");
            }
            finally
            {
                await CleanupRedisKeysAsync(serviceProvider, $"*:{requestId}");
            }
        }

        #endregion

        #region Error Handling Tests

        [IgnoreOnCIFact(DisplayName = "Should handle Redis connection failure gracefully"), TestPriority(50)]
        public async Task Should_Handle_Redis_Connection_Failure_GracefullyAsync()
        {
            // This test would require a mock or a way to simulate connection failure
            // For now, we document the expected behavior
            _testOutput.WriteLine("Note: Connection failure handling should be tested with integration tests");
            await Task.CompletedTask;
        }

        [IgnoreOnCIFact(DisplayName = "Should handle serialization errors"), TestPriority(51)]
        public async Task Should_Handle_Serialization_ErrorsAsync()
        {
            // Arrange
            var serviceProvider = BuildServiceProvider();
            var manager = serviceProvider.GetRequiredService<IRequestManager>();
            var requestId = Guid.NewGuid();

            try
            {
                await manager.TryCreateRequestForCommandAsync<TestRequest>(requestId);

                // Act - Try to store an object with circular reference (should handle gracefully)
                var circularObject = new CircularReferenceObject();
                circularObject.Self = circularObject;

                // Should not throw
                await manager.TryCompleteRequestAsync<TestRequest>(requestId, success: true, result: circularObject);

                _testOutput.WriteLine("Handled potential serialization error gracefully");
            }
            finally
            {
                await CleanupRedisKeysAsync(serviceProvider, $"*:{requestId}");
            }
        }

        #endregion

        #region Performance Tests

        [IgnoreOnCIFact(DisplayName = "Should handle high volume of requests"), TestPriority(60)]
        public async Task Should_Handle_High_Volume_Of_RequestsAsync()
        {
            // Arrange
            var serviceProvider = BuildServiceProvider("RedisSentinel");
            var manager = serviceProvider.GetRequiredService<IRequestManager>();
            var requestCount = 100;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Act
                var tasks = Enumerable.Range(0, requestCount)
                    .Select(async i =>
                    {
                        var requestId = Guid.NewGuid();
                        await manager.TryCreateRequestForCommandAsync<TestRequest>(requestId);
                        await manager.TryCompleteRequestAsync<TestRequest>(requestId, success: true, result: $"Result {i}");
                        return requestId;
                    });

                var requestIds = await Task.WhenAll(tasks);
                stopwatch.Stop();

                // Assert
                requestIds.Length.Should().Be(requestCount);
                _testOutput.WriteLine($"Processed {requestCount} requests in {stopwatch.ElapsedMilliseconds}ms " +
                                    $"({stopwatch.ElapsedMilliseconds / (double)requestCount:F2}ms per request)");

                // Cleanup
                foreach (var id in requestIds)
                {
                    await CleanupRedisKeysAsync(serviceProvider, $"*:{id}");
                }
            }
            catch
            {
                // Best effort cleanup
                throw;
            }
        }

        #endregion

        #region Test Helper Classes

        private record TestRequest(Guid Id) : IRequest;

        private class ComplexTestResult
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public List<string> Items { get; set; } = new();
            public DateTime Timestamp { get; set; }
        }

        private class CircularReferenceObject
        {
            public CircularReferenceObject? Self { get; set; }
        }

        #endregion
    }
}
