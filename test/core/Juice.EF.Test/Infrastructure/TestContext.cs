using System;
using Juice.EF.Test.Domain;
using Microsoft.EntityFrameworkCore;

namespace Juice.EF.Test.Infrastructure
{
    public class TestDbOptions : DbOptions
    {

    }

    public class TestContext : DbContextBase
    {
        public const string SCHEMA = "Contents";
        public DbSet<Content> Contents { get; set; }

        public TestContext(IServiceProvider serviceProvider, DbContextOptions<TestContext> options) : base(serviceProvider, options)
        {
        }

        protected override void ConfigureModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Content>(entity =>
            {
                entity.ToTable(nameof(Content), SCHEMA);

                new DynamicEntityConfiguration<Content, Guid>().Configure(entity);

                entity.Property(m => m.Code).HasMaxLength(DefinedLengh.NameLength);

                #region Indexing
                entity.HasIndex(nameof(Content.Code))
                    .IncludeProperties(m => new { m.Name })
                    .IsUnique();

                entity.HasIndex(nameof(Content.CreatedUser))
                    .HasFilter($"[{nameof(Content.CreatedUser)}] is not null")
                    .IncludeProperties(m => new { m.Name, m.Code, m.CreatedDate });
                #endregion
            });
        }
    }
}
