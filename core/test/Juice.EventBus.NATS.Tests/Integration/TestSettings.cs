using System;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Juice.EventBus.NATS.Tests.Integration
{
    /// <summary>
    /// Optional integration settings, read from appsettings.json, user secrets (written by
    /// <c>test/infra/setup-test-infra.ps1</c>) and environment variables, like the other test projects.
    /// </summary>
    internal static class TestSettings
    {
        private static readonly Lazy<IConfiguration> _configuration = new(() => new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets(typeof(TestSettings).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build());

        private static IConfiguration Configuration => _configuration.Value;

        private static string? Get(string key, string environmentVariable)
        {
            var value = Environment.GetEnvironmentVariable(environmentVariable);
            return string.IsNullOrEmpty(value) ? Configuration[key] : value;
        }

        /// <summary>
        /// Name of the nats docker container; enables the server-restart steps.
        /// </summary>
        public static string? DockerContainer => Get("Nats:DockerContainer", "NATS_DOCKER_CONTAINER");

        public static string? AuthUrl => Get("Nats:Auth:Url", "NATS_AUTH_URL");
        public static string? AuthUserName => Get("Nats:Auth:UserName", "NATS_AUTH_USER");
        public static string? AuthPassword => Get("Nats:Auth:Password", "NATS_AUTH_PASSWORD");

        /// <summary>
        /// A connection string for a new, uniquely named outbox database. Uses
        /// <c>ConnectionStrings:SqlServerConnection</c> (or <c>NATS_TESTS_SQLSERVER</c>, a server name or a full
        /// connection string), falling back to SQL Server LocalDB.
        /// </summary>
        public static string NewSqlServerConnectionString()
        {
            var configured = Get("ConnectionStrings:SqlServerConnection", "NATS_TESTS_SQLSERVER");
            var builder = string.IsNullOrEmpty(configured)
                ? new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", IntegratedSecurity = true }
                : configured.Contains('=')
                    ? new SqlConnectionStringBuilder(configured)
                    : new SqlConnectionStringBuilder { DataSource = configured, IntegratedSecurity = true };
            builder.InitialCatalog = $"JuiceNatsTests_{Guid.NewGuid():N}";
            builder.TrustServerCertificate = true;
            return builder.ConnectionString;
        }
    }
}
