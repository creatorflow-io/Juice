﻿// <auto-generated />
using System;
using Juice.MultiTenant.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Juice.MultiTenant.EF.SqlServer.Migrations.TenantStore
{
    [DbContext(typeof(TenantStoreDbContext))]
    [Migration("20221206084253_InitTenantStoreDb")]
    partial class InitTenantStoreDb
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.1")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder, 1L, 1);

            modelBuilder.Entity("Juice.MultiTenant.Tenant", b =>
                {
                    b.Property<string>("Id")
                        .HasMaxLength(64)
                        .HasColumnType("nvarchar(64)");

                    b.Property<string>("ConnectionString")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTimeOffset>("CreatedDate")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("datetimeoffset")
                        .HasDefaultValue(new DateTimeOffset(new DateTime(2022, 12, 6, 15, 42, 53, 318, DateTimeKind.Unspecified).AddTicks(4170), new TimeSpan(0, 7, 0, 0, 0)));

                    b.Property<string>("CreatedUser")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<bool>("Disabled")
                        .HasColumnType("bit");

                    b.Property<string>("Identifier")
                        .HasMaxLength(16)
                        .HasColumnType("nvarchar(16)");

                    b.Property<DateTimeOffset?>("ModifiedDate")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("ModifiedUser")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("SerializedProperties")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasColumnType("nvarchar(max)")
                        .HasDefaultValue("'{}'");

                    b.HasKey("Id");

                    b.HasIndex("Identifier")
                        .IsUnique()
                        .HasFilter("[Identifier] IS NOT NULL");

                    b.ToTable("Tenant", "App");
                });
#pragma warning restore 612, 618
        }
    }
}
