using System;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using Juice.EF.Extensions;
using Juice.Extensions.DependencyInjection;
using Juice.MultiTenant.EF;
using Juice.MultiTenant.Tests.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.MultiTenant.Tests.Infrastructure
{
    public class TenantContentDbContext : MultiTenantDbContext
    {
        public DbSet<TenantContent> TenantContents { get; set; }
        public TenantContentDbContext(IServiceProvider serviceProvider, DbContextOptions options) : base(options)
        {
            ConfigureServices(serviceProvider);
        }

        protected override void ConfigureModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TenantContent>(options =>
            {
                options.ToTable(nameof(TenantContent), Schema);
                options.IsMultiTenant();
                options.IsAuditable();
            });
        }
    }
    public class TenantContentSqlServerDbContext : TenantContentDbContext
    {
        public TenantContentSqlServerDbContext(IServiceProvider serviceProvider, DbContextOptions options) : base(serviceProvider, options)
        {
        }
    }
    public class TenantContentPostgreDbContext : TenantContentDbContext
    {
        public TenantContentPostgreDbContext(IServiceProvider serviceProvider, DbContextOptions options) : base(serviceProvider, options)
        {
        }
    }

    public class TenantContentSqlServerDbContextFactory : IDesignTimeDbContextFactory<TenantContentSqlServerDbContext>
    {
        public TenantContentSqlServerDbContext CreateDbContext(string[] args)
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {

                // Register DbContext class
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();

                var configuration = configService.GetConfiguration(args);

                services.AddTenantContentDbContext(configuration, "SqlServer", "Contents");
            });

            return resolver.ServiceProvider.GetRequiredService<TenantContentSqlServerDbContext>();
        }
    }

    public class TenantContentPostgreDbContextFactory : IDesignTimeDbContextFactory<TenantContentPostgreDbContext>
    {
        public TenantContentPostgreDbContext CreateDbContext(string[] args)
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {

                // Register DbContext class
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();

                var configuration = configService.GetConfiguration(args);

                services.AddTenantContentDbContext(configuration, "PostgreSQL", "Contents");
            });

            return resolver.ServiceProvider.GetRequiredService<TenantContentPostgreDbContext>();
        }
    }
}
