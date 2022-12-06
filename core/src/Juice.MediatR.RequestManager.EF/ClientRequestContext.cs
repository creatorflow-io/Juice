﻿using Juice.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Juice.MediatR.RequestManager.EF
{
    public class ClientRequestContext : DbContext, ISchemaDbContext
    {
        public string? Schema { get; private set; }
        public ClientRequestContext(DbOptions<ClientRequestContext> dbOptions,
            DbContextOptions<ClientRequestContext> options) : base(options)
        {
            Schema = dbOptions?.Schema;
        }

        public DbSet<ClientRequest> ClientRequests { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<ClientRequest>(ConfigureClientRequest);
        }

        private void ConfigureClientRequest(EntityTypeBuilder<ClientRequest> builder)
        {
            builder.ToTable("ClientRequest", Schema);

            builder.HasKey(e => e.Id);

            builder.Property(e => e.Name)
                .IsRequired();

            builder.Property(e => e.Time)
                .HasDefaultValue(DateTimeOffset.Now)
                .IsRequired();

            builder.Property(e => e.State)
                .IsRequired();

        }
    }
}
