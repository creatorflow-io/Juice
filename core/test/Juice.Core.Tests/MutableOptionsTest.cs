using System;
using System.Threading.Tasks;
using Juice.Core.Tests.Models;
using Juice.Extensions.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Juice.Core.Tests
{
    public class MutableOptionsTest
    {
        private readonly ITestOutputHelper _output;

        public MutableOptionsTest(ITestOutputHelper testOutput)
        {
            _output = testOutput;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }


        [Fact(DisplayName = "Write config to appsettings")]
        public async Task Config_should_write_Async()
        {
            var builder = WebApplication.CreateBuilder();
            var services = builder.Services;
            var configuration = builder.Configuration;

            services.AddSingleton(_output);

            services.AddLogging(builder =>
            {
                builder.ClearProviders()
                .AddTestOutputLogger()
                .AddConfiguration(configuration.GetSection("Logging"));
            });

            services.UseOptionsMutableFileStore("appsettings.Development.json");

            services.ConfigureMutable<Options>(configuration.GetSection("Options"));

            var serviceProvider = builder.Build().Services;
            using var scope = serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope();
            var options = scope.ServiceProvider.GetRequiredService<IOptionsMutable<Options>>();
            var time = DateTimeOffset.Now.ToString();
            Assert.True(await options.UpdateAsync(o => o.Time = time));
            Assert.Equal(time, options.Value.Time);
        }

        [Fact(DisplayName = "Separated appsettings")]
        public async Task Config_should_write_to_separated_Async()
        {
            var builder = WebApplication.CreateBuilder();

            builder.Configuration.AddJsonFile($"appsettings.Separated.{builder.Environment.EnvironmentName}.json", true);

            var services = builder.Services;
            var configuration = builder.Configuration;

            services.AddSingleton(_output);

            services.AddLogging(builder =>
            {
                builder.ClearProviders()
                .AddTestOutputLogger()
                .AddConfiguration(configuration.GetSection("Logging"));
            });

            services.UseOptionsMutableFileStore<Options>($"appsettings.Separated.{builder.Environment.EnvironmentName}.json");

            services.ConfigureMutable<Options>(configuration.GetSection("Options"));

            using var scope = builder.Build().Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
            var serviceProvider = scope.ServiceProvider;
            var options = serviceProvider.GetRequiredService<IOptionsMutable<Options>>();
            var time = DateTimeOffset.Now.ToString();

            Assert.NotNull(options.Value.Time);
            Assert.True(await options.UpdateAsync(o => o.Time = time));
            Assert.Equal(time, options.Value.Time);
        }

    }
}
