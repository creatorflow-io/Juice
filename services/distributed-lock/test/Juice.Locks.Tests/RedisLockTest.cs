using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.Extensions.DependencyInjection;
using Juice.Locks.Redis;
using Juice.XUnit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Juice.Locks.Tests
{
    public class RedisLockTest
    {
        private readonly ITestOutputHelper _output;

        public RedisLockTest(ITestOutputHelper testOutput)
        {
            _output = testOutput;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }

        [IgnoreOnCITheory(DisplayName = "Should lock single instance"), TestPriority(999)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        public void Object_should_lock(int parallelInstances)
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

                services.AddSingleton(provider => _output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddRedLock(options => options.ConnectionString = configuration.GetConnectionString("Redis")
                    ?? throw new ArgumentNullException("Redis connection string"));
            });

            var locker = resolver.ServiceProvider.GetRequiredService<IDistributedLock>();

            var lockCounds = new int[4];
            var lockTypes = new string[] { "food", "orange", "apple", "chiken" };
            Parallel.For(0, 3, x =>
            {
                var lockObj = new LockObj();
                string food = lockTypes[x];

                Parallel.For(0, parallelInstances, y =>
                {
                    string person = $"person:{y}";
                    using var @lock = locker.AcquireLock(lockObj, TimeSpan.FromMinutes(1), obj => obj.Id, person);

                    if (@lock != null)
                    {
                        Interlocked.Increment(ref lockCounds[x]);
                        _output.WriteLine($"{person} begin eat {food}(with lock) at {DateTimeOffset.Now.ToUnixTimeMilliseconds()}.");
                        if (new Random().NextDouble() < 0.6)
                        {
                            var released = locker.ReleaseLock(@lock);

                            _output.WriteLine($"{person} release lock {released}  {DateTimeOffset.Now.ToUnixTimeMilliseconds()}");
                        }
                        else
                        {
                            _output.WriteLine($"{person} do not release lock ....");
                        }
                        Interlocked.Decrement(ref lockCounds[x]);
                    }
                    else
                    {
                        _output.WriteLine($"{person} begin eat {food}(without lock) at {DateTimeOffset.Now.ToUnixTimeMilliseconds()}.");
                    }
                });

                lockCounds[x].Should().BeInRange(0, 1);
            });

        }

        [IgnoreOnCIFact(DisplayName = "Should lock async")]
        public async Task Object_should_lock_Async()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

                services.AddSingleton(provider => _output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddRedLock(options => options.ConnectionString = configuration.GetConnectionString("Redis"));
            });

            var locker = resolver.ServiceProvider.GetRequiredService<IDistributedLock>();


            int lockCount = 0;

            var lockObj = new LockObj();

            var result = Parallel.For(0, 5, async y =>
            {
                string person = $"person:{y}";
                string food = "food";
                using var @lock = await locker.AcquireLockAsync(lockObj, TimeSpan.FromMinutes(1), obj => obj.Id, person);

                if (@lock != null)
                {
                    Interlocked.Increment(ref lockCount);
                    _output.WriteLine($"{person} begin eat {food}(with lock) at {DateTimeOffset.Now.ToUnixTimeMilliseconds()}.");
                    if (new Random().NextDouble() < 0.6)
                    {
                        var released = await locker.ReleaseLockAsync(@lock);

                        _output.WriteLine($"{person} release lock {released}  {DateTimeOffset.Now.ToUnixTimeMilliseconds()}");
                    }
                    else
                    {
                        _output.WriteLine($"{person} do not release lock ....");
                    }
                    Interlocked.Decrement(ref lockCount);
                }
                else
                {
                    _output.WriteLine($"{person} begin eat {food}(without lock) at {DateTimeOffset.Now.ToUnixTimeMilliseconds()}.");
                }
            });
            var now = DateTimeOffset.Now;
            while (!result.IsCompleted || (DateTimeOffset.Now - now) < TimeSpan.FromSeconds(3))
            {
                await Task.Delay(100);
            }

            lockCount.Should().BeInRange(0, 1);
        }
    }

    internal class LockObj
    {
        public Guid Id { get; init; } = Guid.NewGuid();
    }
}
