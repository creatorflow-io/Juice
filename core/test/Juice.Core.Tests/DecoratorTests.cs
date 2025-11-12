using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Juice.Core.Tests
{
    public class DecoratorTests
    {
        private ITestOutputHelper _output;

        public DecoratorTests(ITestOutputHelper output)
        {
            _output = output;
        }
        [Fact]
        public void Service_by_instanceAsync()
        {
            var dependencyResolver = DependencyResolver.Create(services =>
            {
                services.AddSingleton(_output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                        .AddTestOutputLogger();
                });

                services.AddSingleton<ITestService>(new TestService());
                services.Decorate<ITestService, TestServiceDecorator>();
            });

            var serviceProvider = dependencyResolver.ServiceProvider;
            var testService = serviceProvider.GetRequiredService<ITestService>();
            testService.Test().Should().Be(2);
        }

        [Fact]
        public void Services_by_instanceAsync()
        {
            var dependencyResolver = DependencyResolver.Create(services =>
            {
                services.AddSingleton(_output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                        .AddTestOutputLogger();
                });

                services.AddSingleton<ITestService>(new TestService());
                services.AddSingleton<ITestService>(new TestService2());
                services.Decorate<ITestService, TestServiceDecorator>();
            });

            var serviceProvider = dependencyResolver.ServiceProvider;
            var testServices = serviceProvider.GetServices<ITestService>();
            testServices.Count().Should().Be(2);
            testServices.First().Test().Should().Be(2);
            testServices.Last().Test().Should().Be(4);
        }

        [Fact]
        public void Service_by_factoryAsync()
        {
            var dependencyResolver = DependencyResolver.Create(services =>
            {
                services.AddSingleton(_output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                        .AddTestOutputLogger();
                });

                services.AddSingleton<ITestService>(sp => new TestService());
                services.Decorate<ITestService, TestServiceDecorator>();
            });

            var serviceProvider = dependencyResolver.ServiceProvider;
            var testService = serviceProvider.GetRequiredService<ITestService>();
            testService.Test().Should().Be(2);
        }

        [Fact]
        public void Services_by_factoryAsync()
        {
            var dependencyResolver = DependencyResolver.Create(services =>
            {
                services.AddSingleton(_output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                        .AddTestOutputLogger();
                });

                services.AddSingleton<ITestService>(sp => new TestService());
                services.AddSingleton<ITestService>(sp => new TestService2());
                services.Decorate<ITestService, TestServiceDecorator>();
            });

            var serviceProvider = dependencyResolver.ServiceProvider;
            var testServices = serviceProvider.GetServices<ITestService>();
            testServices.Count().Should().Be(2);
            testServices.First().Test().Should().Be(2);
            testServices.Last().Test().Should().Be(4);
        }

        [Fact]
        public void Service_by_typeAsync()
        {
            var dependencyResolver = DependencyResolver.Create(services =>
            {
                services.AddSingleton(_output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                        .AddTestOutputLogger();
                });

                services.AddSingleton<ITestService, TestService>();
                services.Decorate<ITestService, TestServiceDecorator>();
            });

            var serviceProvider = dependencyResolver.ServiceProvider;
            var testService = serviceProvider.GetRequiredService<ITestService>();
            testService.Test().Should().Be(2);
        }

        [Fact]
        public void Services_by_typeAsync()
        {
            var dependencyResolver = DependencyResolver.Create(services =>
            {
                services.AddSingleton(_output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                        .AddTestOutputLogger();
                });

                services.AddSingleton<ITestService, TestService>();
                services.AddSingleton<ITestService, TestService2>();
                services.Decorate<ITestService, TestServiceDecorator>();
            });

            var serviceProvider = dependencyResolver.ServiceProvider;
            var testServices = serviceProvider.GetServices<ITestService>();
            testServices.Count().Should().Be(2);
            testServices.First().Test().Should().Be(2);
            testServices.Last().Test().Should().Be(4);
        }
    }

    internal interface ITestService
    {
        int Test();
    }

    internal class TestService : ITestService
    {
        public int Test()
        {
            return 1;
        }
    }

    internal class TestService2 : ITestService { public int Test() => 2;}

    internal class TestServiceDecorator : ITestService
    {
        private readonly ITestService _testService;
        private readonly ILogger _logger;

        public TestServiceDecorator(ITestService testService, ILogger<TestServiceDecorator> logger)
        {
            _testService = testService;
            _logger = logger;
        }

        public int Test()
        {
            var original = _testService.Test();
            _logger.LogInformation("Original value: {0}", original);
            return original * 2;
        }
    }
}
