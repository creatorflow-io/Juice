using FluentAssertions;
using Juice.Extensions.DependencyInjection;
using Juice.Extensions.Redis;
using Juice.Messaging.Idempotency;
using Juice.Messaging.Idempotency.EF;
using Juice.Messaging.Outbox;
using Juice.XUnit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Xunit;

namespace Juice.Messaging.Tests
{
    [TestCaseOrderer(typeof(Juice.XUnit.PriorityOrderer))]
    public class IdempotencyServiceTest
    {
        private readonly ITestOutputHelper _testOutput;

        public IdempotencyServiceTest(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
        }

        #region Setup Helpers

        private IServiceProvider BuildServiceProvider(string provider, IDeliveryNodeIdentity? nodeIdentity = null)
        {
            var resolver = DependencyResolver.Create((services, configuration) =>
            {
                var builder = services.AddMessaging();
                switch (provider)
                {
                    case "Redis":
                        builder.AddIdempotencyRedis(options =>
                        {
                            options.ConnectionString = configuration.GetConnectionString("RedisSentinel");
                        });
                        break;
                    case "SqlServer":
                    case "PostgreSQL":
                        builder.AddIdempotencyEF(configuration, options =>
                        {
                            options.DatabaseProvider = provider;
                            options.ConnectionName = provider == "SqlServer" ? "SqlServerConnection" : "PostgreConnection";
                            options.Schema = "App";
                        });
                        break;
                    case "DistributedCache":
                        builder.AddIdempotencyDistributedCache();
                        services.AddDistributedMemoryCache();
                        break;
                    default:
                        builder.AddIdempotencyInMemory();
                        break;
                }

                if (nodeIdentity != null)
                {
                    services.AddSingleton(nodeIdentity);
                }

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger(_testOutput)
                    .AddConfiguration(configuration.GetSection("Logging"));
                });
            }, default);

            return resolver.ServiceProvider;
        }

        private async Task CleanupAsync(IServiceProvider serviceProvider, string scope, string key)
        {
            await CleanupRedisKeysAsync(serviceProvider, $"*:{key}");
            await CleanupEFStoreAsync(serviceProvider, scope, key);
        }

        private async Task CleanupRedisKeysAsync(IServiceProvider serviceProvider, params string[] keyPatterns)
        {
            try
            {
                var connectionProvider = serviceProvider.GetService<IRedisConnectionProvider<Idempotency.Redis.RedisIdempotencyService>>();
                if (connectionProvider == null) return;

                using var connection = await connectionProvider.GetConnectionAsync();
                var server = connection.GetServer(connection.GetEndPoints().First());
                var db = connection.GetDatabase();

                const string deleteScript = @"
local cursor = '0' repeat local res = redis.call('SCAN', cursor, 'MATCH', ARGV[1], 'COUNT', 100) cursor = res[1] for i, key in ipairs(res[2]) do redis.call('DEL', key) end until cursor == '0' return 1 ";

                foreach (var pattern in keyPatterns)
                {
                    await db.ScriptEvaluateAsync(deleteScript, values: new RedisValue[] { pattern });
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        private async Task CleanupEFStoreAsync(IServiceProvider serviceProvider, string scope, string key)
        {
            try
            {
                using var scoppedProvider = serviceProvider.CreateScope();
                var dbContext = scoppedProvider.ServiceProvider.GetService<IdempotencyContext>();
                if (dbContext == null) return;

                await dbContext.IdempotencyRecords
                    .Where(r => r.Scope == scope && r.Key == key)
                    .ExecuteDeleteAsync();
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        #endregion

        #region Connection Tests

        [IgnoreOnCITheory(DisplayName = "Redis connect"), TestPriority(1)]
        [InlineData("Redis")]
        public async Task Redis_ConnectAsync(string provider)
        {
            // Arrange
            var serviceProvider = BuildServiceProvider(provider);

            // Act
            var manager = serviceProvider.GetRequiredService<IIdempotencyService>();
            var connectionProvider = serviceProvider.GetRequiredService<IRedisConnectionProvider<Idempotency.Redis.RedisIdempotencyService>>();
            using var connection = await connectionProvider.GetConnectionAsync();

            // Assert
            connection.Should().NotBeNull();
            connection.IsConnected.Should().BeTrue();
            manager.Should().NotBeNull();

            _testOutput.WriteLine($"Redis connection established for provider: {provider}");
        }

        [IgnoreOnCITheory(DisplayName = "EF migration"), TestPriority(10)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Schema_MigrationAsync(string provider)
        {
            // Arrange
            using var resolver = BuildServiceProvider(provider).CreateScope();

            // Act
            var context = resolver.ServiceProvider.GetRequiredService<IdempotencyContext>();
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();

            if (pendingMigrations.Any())
            {
                _testOutput.WriteLine($"[{provider}] You have {pendingMigrations.Count()} pending migrations to apply.");
                _testOutput.WriteLine($"[{provider}] Applying pending migrations now");
                await context.Database.MigrateAsync();
            }

            // Assert
            var historyTable = await context.Database.GetAppliedMigrationsAsync();
            historyTable.Count().Should().BeGreaterThan(0);

            _testOutput.WriteLine($"[{provider}] Applied {historyTable.Count()} migrations");
        }
        #endregion

        #region Request Creation Tests

        [IgnoreOnCITheory(DisplayName = "Should create request marker"), TestPriority(10)]
        [InlineData("Redis")]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Should_Create_Request_MarkerAsync(string provider)
        {
            // Arrange
            var serviceProvider = BuildServiceProvider(provider);
            var manager = serviceProvider.GetRequiredService<IIdempotencyService>();
            var requestId = Guid.NewGuid().ToString();

            try
            {
                // Act
                var created = await manager.TryCreateRequestAsync("TestRequest", requestId);

                // Assert
                created.Succeeded.Should().BeTrue();
                _testOutput.WriteLine($"[{provider}] Created request marker for ID: {requestId}");
            }
            finally
            {
                await CleanupAsync(serviceProvider, "TestRequest", requestId);
            }
        }

        [IgnoreOnCITheory(DisplayName = "Should prevent duplicate request creation"), TestPriority(11)]
        [InlineData("Redis")]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Should_Prevent_Duplicate_Request_CreationAsync(string provider)
        {
            // Arrange
            var serviceProvider = BuildServiceProvider(provider);
            var manager = serviceProvider.GetRequiredService<IIdempotencyService>();
            var requestId = Guid.NewGuid().ToString();

            try
            {
                // Act
                var created1 = await manager.TryCreateRequestAsync("TestRequest", requestId);
                var created2 = await manager.TryCreateRequestAsync("TestRequest", requestId);

                // Assert
                created1.Succeeded.Should().BeTrue("First creation should succeed");
                created2.Succeeded.Should().BeFalse("Second creation should fail (duplicate)");

                _testOutput.WriteLine($"[{provider}] First attempt: {created1.Succeeded}, Second attempt: {created2.Succeeded}");
            }
            finally
            {
                await CleanupAsync(serviceProvider, "TestRequest", requestId);
            }
        }

        [IgnoreOnCITheory(DisplayName = "Should handle concurrent request creation"), TestPriority(12)]
        [InlineData("Redis")]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Should_Handle_Concurrent_Request_CreationAsync(string provider)
        {
            // Arrange
            var serviceProvider = BuildServiceProvider(provider);
            var requestId = Guid.NewGuid().ToString();

            try
            {
                // Act - Simulate concurrent requests
                var tasks = Enumerable.Range(0, 10)
                    .Select(async i =>
                    {
                        using var scope = serviceProvider.CreateScope();
                        var manager = scope.ServiceProvider.GetRequiredService<IIdempotencyService>();
                        await Task.Delay(Random.Shared.Next(10, 50)); // Add jitter
                        return await manager.TryCreateRequestAsync("TestRequest", requestId);
                    });

                var results = await Task.WhenAll(tasks);

                // Assert
                results.Count(r => r.Succeeded).Should().Be(1, "Only one creation should succeed");
                results.Count(r => !r.Succeeded).Should().Be(9, "Nine attempts should fail");

                _testOutput.WriteLine($"[{provider}] Successful: {results.Count(r => r.Succeeded)}, Failed: {results.Count(r => !r.Succeeded)}");
            }
            finally
            {
                await CleanupAsync(serviceProvider, "TestRequest", requestId);
            }
        }

        #endregion

        #region Request Completion Tests

        [IgnoreOnCITheory(DisplayName = "Should complete request successfully without result"), TestPriority(20)]
        [InlineData("Redis")]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        [InlineData("InMemory")]
        [InlineData("DistributedCache")]
        public async Task Should_Complete_Request_Successfully_Without_ResultAsync(string provider)
        {
            // Arrange
            var serviceProvider = BuildServiceProvider(provider);
            var manager = serviceProvider.GetRequiredService<IIdempotencyService>();
            var requestId = Guid.NewGuid().ToString();

            try
            {
                await manager.TryCreateRequestAsync("TestRequest", requestId);

                // Act
                await manager.TryCompleteRequestAsync("TestRequest", requestId, success: true, result: null);

                // Assert
                var verifyCreate = await manager.TryCreateRequestAsync("TestRequest", requestId);
                verifyCreate.Succeeded.Should().BeFalse("Request should be marked as completed");

                _testOutput.WriteLine($"[{provider}] Request completed successfully without result");
            }
            finally
            {
                await CleanupAsync(serviceProvider, "TestRequest", requestId);
            }
        }

        [IgnoreOnCITheory(DisplayName = "Should complete request and cache result"), TestPriority(21)]
        [InlineData("Redis")]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        [InlineData("InMemory")]
        [InlineData("DistributedCache")]
        public async Task Should_Complete_Request_And_Cache_ResultAsync(string provider)
        {
            // Arrange
            var serviceProvider = BuildServiceProvider(provider);
            var manager = serviceProvider.GetRequiredService<IIdempotencyService>();
            var requestId = Guid.NewGuid().ToString();
            var expectedResult = OperationResult.Result("Test data", "Operation succeeded");

            try
            {
                await manager.TryCreateRequestAsync("TestRequest", requestId);

                // Act
                await manager.TryCompleteRequestAsync("TestRequest", requestId, success: true, result: expectedResult);

                // Assert - Verify result is cached
                var cachedResult = await manager.TryCreateRequestAsync<IOperationResult<string>>("TestRequest", requestId);
                cachedResult.HasData.Should().BeTrue("Cached result should exist");
                cachedResult.DataValue.Should().NotBeNull();
                cachedResult.DataValue!.Data.Should().Be("Test data");

                _testOutput.WriteLine($"[{provider}] Cached result retrieved: {cachedResult.DataValue.Message}");
            }
            finally
            {
                await CleanupAsync(serviceProvider, "TestRequest", requestId);
            }
        }

        [IgnoreOnCITheory(DisplayName = "Should handle failed request completion"), TestPriority(22)]
        [InlineData("Redis")]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        [InlineData("InMemory")]
        [InlineData("DistributedCache")]
        public async Task Should_Handle_Failed_Request_CompletionAsync(string provider)
        {
            // Arrange
            var serviceProvider = BuildServiceProvider(provider);
            var manager = serviceProvider.GetRequiredService<IIdempotencyService>();
            var requestId = Guid.NewGuid().ToString();

            try
            {
                await manager.TryCreateRequestAsync("TestRequest", requestId);

                // Act
                await manager.TryCompleteRequestAsync("TestRequest", requestId, success: false, result: null);

                // Assert - Request should be recreatable after failure
                var recreate = await manager.TryCreateRequestAsync("TestRequest", requestId);
                recreate.Succeeded.Should().BeTrue("Failed request should allow recreation");

                _testOutput.WriteLine($"[{provider}] Failed request allows recreation");
            }
            finally
            {
                await CleanupAsync(serviceProvider, "TestRequest", requestId);
            }
        }

        #endregion

        #region Result Caching Tests

        [IgnoreOnCITheory(DisplayName = "Should retrieve cached string result"), TestPriority(30)]
        [InlineData("Redis")]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        [InlineData("InMemory")]
        [InlineData("DistributedCache")]
        public async Task Should_Retrieve_Cached_String_ResultAsync(string provider)
        {
            // Arrange
            var serviceProvider = BuildServiceProvider(provider);
            var manager = serviceProvider.GetRequiredService<IIdempotencyService>();
            var requestId = Guid.NewGuid().ToString();
            var expectedResult = "Test Result String";

            try
            {
                await manager.TryCreateRequestAsync("TestRequest", requestId);
                await manager.TryCompleteRequestAsync("TestRequest", requestId, success: true, result: expectedResult);

                // Act
                var create = await manager.TryCreateRequestAsync<string>("TestRequest", requestId);

                // Assert
                create.HasData.Should().BeTrue("Cached result should be retrieved");
                var cachedResult = create.DataValue;
                cachedResult.Should().NotBeNull();
                cachedResult.Should().Be(expectedResult);

                _testOutput.WriteLine($"[{provider}] Retrieved cached result: {cachedResult}");
            }
            finally
            {
                await CleanupAsync(serviceProvider, "TestRequest", requestId);
            }
        }

        [IgnoreOnCITheory(DisplayName = "Should retrieve cached IOperationResult"), TestPriority(31)]
        [InlineData("Redis")]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        [InlineData("InMemory")]
        [InlineData("DistributedCache")]
        public async Task Should_Retrieve_Cached_IOperationResultAsync(string provider)
        {
            // Arrange
            var serviceProvider = BuildServiceProvider(provider);
            var manager = serviceProvider.GetRequiredService<IIdempotencyService>();
            var requestId = Guid.NewGuid().ToString();
            var expectedResult = OperationResult.Result("Success data", "Operation completed");

            try
            {
                await manager.TryCreateRequestAsync("TestRequest", requestId);
                await manager.TryCompleteRequestAsync("TestRequest", requestId, success: true, result: expectedResult);

                // Act
                var create = await manager.TryCreateRequestAsync<IOperationResult<string>>("TestRequest", requestId);

                // Assert
                create.HasData.Should().BeTrue("Cached result should be retrieved");
                var cachedResult = create.DataValue;
                cachedResult.Should().NotBeNull();
                cachedResult!.Succeeded.Should().BeTrue();
                cachedResult.Data.Should().Be("Success data");
                cachedResult.Message.Should().Be("Operation completed");

                _testOutput.WriteLine($"[{provider}] Retrieved cached operation result: {cachedResult.Message}");
            }
            finally
            {
                await CleanupAsync(serviceProvider, "TestRequest", requestId);
            }
        }

        [IgnoreOnCITheory(DisplayName = "Should handle complex object caching"), TestPriority(33)]
        [InlineData("Redis")]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        [InlineData("InMemory")]
        [InlineData("DistributedCache")]
        public async Task Should_Handle_Complex_Object_CachingAsync(string provider)
        {
            // Arrange
            var serviceProvider = BuildServiceProvider(provider);
            var manager = serviceProvider.GetRequiredService<IIdempotencyService>();
            var requestId = Guid.NewGuid().ToString();
            var expectedResult = new ComplexTestResult
            {
                Id = Guid.NewGuid(),
                Name = "Test Name",
                Items = new List<string> { "Item1", "Item2", "Item3" },
                Timestamp = DateTime.UtcNow
            };

            try
            {
                await manager.TryCreateRequestAsync("TestRequest", requestId);
                await manager.TryCompleteRequestAsync("TestRequest", requestId, success: true, result: expectedResult);

                // Act
                var create = await manager.TryCreateRequestAsync<ComplexTestResult>("TestRequest", requestId);

                // Assert
                create.HasData.Should().BeTrue("Cached result should be retrieved");
                var cachedResult = create.DataValue;
                cachedResult.Should().NotBeNull();
                cachedResult!.Id.Should().Be(expectedResult.Id);
                cachedResult.Name.Should().Be(expectedResult.Name);
                cachedResult.Items.Should().BeEquivalentTo(expectedResult.Items);

                _testOutput.WriteLine($"[{provider}] Retrieved complex object: {cachedResult.Name} with {cachedResult.Items.Count} items");
            }
            finally
            {
                await CleanupAsync(serviceProvider, "TestRequest", requestId);
            }
        }

        #endregion

        #region TTL and Expiration Tests

        [IgnoreOnCITheory(DisplayName = "Should set TTL on request keys"), TestPriority(40)]
        [InlineData("Redis")]
        public async Task Should_Set_TTL_On_Request_KeysAsync(string provider)
        {
            // Arrange
            var serviceProvider = BuildServiceProvider(provider);
            var manager = serviceProvider.GetRequiredService<IIdempotencyService>();
            var requestId = Guid.NewGuid().ToString();

            try
            {
                // Act
                await manager.TryCreateRequestAsync("TestRequest", requestId);

                // Assert
                var connectionProvider = serviceProvider.GetRequiredService<IRedisConnectionProvider<Idempotency.Redis.RedisIdempotencyService>>();
                using var connection = await connectionProvider.GetConnectionAsync();
                var db = connection.GetDatabase();

                var requestKey = $"request:TestRequest:{requestId}";
                var ttl = await db.KeyTimeToLiveAsync(requestKey);

                ttl.Should().NotBeNull();
                ttl.Value.TotalMinutes.Should().BeGreaterThan(14, "TTL should be close to 15 minutes");

                _testOutput.WriteLine($"[{provider}] Request key TTL: {ttl.Value.TotalMinutes:F2} minutes");
            }
            finally
            {
                await CleanupAsync(serviceProvider, "TestRequest", requestId);
            }
        }

        [IgnoreOnCITheory(DisplayName = "Should set TTL on result keys"), TestPriority(41)]
        [InlineData("Redis")]
        public async Task Should_Set_TTL_On_Result_KeysAsync(string provider)
        {
            // Arrange
            var serviceProvider = BuildServiceProvider(provider);
            var manager = serviceProvider.GetRequiredService<IIdempotencyService>();
            var requestId = Guid.NewGuid().ToString();

            try
            {
                await manager.TryCreateRequestAsync("TestRequest", requestId);

                // Act
                await manager.TryCompleteRequestAsync("TestRequest", requestId, success: true, result: "Test data");

                // Assert
                var connectionProvider = serviceProvider.GetRequiredService<IRedisConnectionProvider<Idempotency.Redis.RedisIdempotencyService>>();
                using var connection = await connectionProvider.GetConnectionAsync();
                var db = connection.GetDatabase();

                var resultKey = $"result:TestRequest:{requestId}";
                var ttl = await db.KeyTimeToLiveAsync(resultKey);

                ttl.Should().NotBeNull();
                ttl.Value.TotalHours.Should().BeGreaterThan(23, "TTL should be close to 24 hours");

                _testOutput.WriteLine($"[{provider}] Result key TTL: {ttl.Value.TotalHours:F2} hours");
            }
            finally
            {
                await CleanupAsync(serviceProvider, "TestRequest", requestId);
            }
        }

        #endregion

        #region Error Handling Tests

        [IgnoreOnCITheory(DisplayName = "Should handle serialization errors"), TestPriority(51)]
        [InlineData("Redis")]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        [InlineData("InMemory")]
        [InlineData("DistributedCache")]
        public async Task Should_Handle_Serialization_ErrorsAsync(string provider)
        {
            // Arrange
            var serviceProvider = BuildServiceProvider(provider);
            var manager = serviceProvider.GetRequiredService<IIdempotencyService>();
            var requestId = Guid.NewGuid().ToString();

            try
            {
                await manager.TryCreateRequestAsync("TestRequest", requestId);

                // Act - Try to store an object with circular reference (should handle gracefully)
                var circularObject = new CircularReferenceObject();
                circularObject.Self = circularObject;

                // Should not throw
                await manager.TryCompleteRequestAsync("TestRequest", requestId, success: true, result: circularObject);

                _testOutput.WriteLine($"[{provider}] Handled potential serialization error gracefully");
            }
            finally
            {
                await CleanupAsync(serviceProvider, "TestRequest", requestId);
            }
        }

        #endregion

        #region Performance Tests

        [IgnoreOnCITheory(DisplayName = "Should handle high volume of requests"), TestPriority(60)]
        [InlineData("Redis")]
        public async Task Should_Handle_High_Volume_Of_RequestsAsync(string provider)
        {
            // Arrange
            var serviceProvider = BuildServiceProvider(provider);
            var manager = serviceProvider.GetRequiredService<IIdempotencyService>();
            var requestCount = 100;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var requestIds = new List<string>();

            try
            {
                // Act
                var tasks = Enumerable.Range(0, requestCount)
                    .Select(async i =>
                    {
                        var requestId = Guid.NewGuid().ToString();
                        await manager.TryCreateRequestAsync("TestRequest", requestId);
                        await manager.TryCompleteRequestAsync("TestRequest", requestId, success: true, result: $"Result {i}");
                        return requestId;
                    });

                requestIds.AddRange(await Task.WhenAll(tasks));
                stopwatch.Stop();

                // Assert
                requestIds.Count.Should().Be(requestCount);
                _testOutput.WriteLine($"[{provider}] Processed {requestCount} requests in {stopwatch.ElapsedMilliseconds}ms " +
                                    $"({stopwatch.ElapsedMilliseconds / (double)requestCount:F2}ms per request)");
            }
            finally
            {
                // Cleanup
                foreach (var id in requestIds)
                {
                    await CleanupAsync(serviceProvider, "TestRequest", id);
                }
            }
        }

        #endregion

        #region ProcessedBy Tests

        [IgnoreOnCITheory(DisplayName = "Should stamp ProcessedBy on create when node identity is registered"), TestPriority(70)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Should_Stamp_ProcessedBy_On_CreateAsync(string provider)
        {
            var nodeIdentity = new FixedNodeIdentity("test-node-create");
            var serviceProvider = BuildServiceProvider(provider, nodeIdentity);
            var manager = serviceProvider.GetRequiredService<IIdempotencyService>();
            var requestId = Guid.NewGuid().ToString();

            try
            {
                await manager.TryCreateRequestAsync("ProcessedByTest", requestId);

                using var scope = serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IdempotencyContext>();
                var record = await db.IdempotencyRecords.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Scope == "ProcessedByTest" && r.Key == requestId);

                record.Should().NotBeNull();
                record!.ProcessedBy.Should().Be("test-node-create");

                _testOutput.WriteLine($"[{provider}] ProcessedBy on create: {record.ProcessedBy}");
            }
            finally
            {
                await CleanupEFStoreAsync(serviceProvider, "ProcessedByTest", requestId);
            }
        }

        [IgnoreOnCITheory(DisplayName = "Should update ProcessedBy on complete when node identity is registered"), TestPriority(71)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Should_Update_ProcessedBy_On_CompleteAsync(string provider)
        {
            var nodeIdentity = new FixedNodeIdentity("test-node-complete");
            var serviceProvider = BuildServiceProvider(provider, nodeIdentity);
            var manager = serviceProvider.GetRequiredService<IIdempotencyService>();
            var requestId = Guid.NewGuid().ToString();

            try
            {
                await manager.TryCreateRequestAsync("ProcessedByTest", requestId);
                await manager.TryCompleteRequestAsync("ProcessedByTest", requestId, success: true, result: null);

                using var scope = serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IdempotencyContext>();
                var record = await db.IdempotencyRecords.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Scope == "ProcessedByTest" && r.Key == requestId);

                record.Should().NotBeNull();
                record!.ProcessedBy.Should().Be("test-node-complete");

                _testOutput.WriteLine($"[{provider}] ProcessedBy on complete: {record.ProcessedBy}");
            }
            finally
            {
                await CleanupEFStoreAsync(serviceProvider, "ProcessedByTest", requestId);
            }
        }

        [IgnoreOnCITheory(DisplayName = "Should leave ProcessedBy null when no node identity is registered"), TestPriority(72)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Should_Leave_ProcessedBy_Null_Without_NodeIdentityAsync(string provider)
        {
            var serviceProvider = BuildServiceProvider(provider);
            var manager = serviceProvider.GetRequiredService<IIdempotencyService>();
            var requestId = Guid.NewGuid().ToString();

            try
            {
                await manager.TryCreateRequestAsync("ProcessedByTest", requestId);
                await manager.TryCompleteRequestAsync("ProcessedByTest", requestId, success: true, result: null);

                using var scope = serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IdempotencyContext>();
                var record = await db.IdempotencyRecords.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Scope == "ProcessedByTest" && r.Key == requestId);

                record.Should().NotBeNull();
                record!.ProcessedBy.Should().BeNull();

                _testOutput.WriteLine($"[{provider}] ProcessedBy without identity: {record.ProcessedBy ?? "(null)"}");
            }
            finally
            {
                await CleanupEFStoreAsync(serviceProvider, "ProcessedByTest", requestId);
            }
        }

        #endregion

        #region Test Helper Classes

        private record TestRequest(Guid Id) : MessageBase(Id), IMessage;

        private sealed class FixedNodeIdentity : IDeliveryNodeIdentity
        {
            public string NodeId { get; }
            public FixedNodeIdentity(string nodeId) => NodeId = nodeId;
        }

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
