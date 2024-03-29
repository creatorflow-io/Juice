﻿// <auto-generated />
using System;
using Juice.EF.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Juice.EF.Tests.Infrastructure;
#nullable disable

namespace Juice.EF.Tests.Migrations
{
    [DbContext(typeof(TestContext))]
    [Migration("20220719100324_InitialCreate")]
    partial class InitialCreate
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder, 1L, 1);

            modelBuilder.Entity("Juice.EF.Tests.Domain.Content", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier")
                        .HasDefaultValueSql("newid()");

                    b.Property<string>("Code")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<DateTimeOffset>("CreatedDate")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("datetimeoffset")
                        .HasDefaultValueSql("SYSDATETIMEOFFSET()");

                    b.Property<string>("CreatedUser")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<bool>("Disabled")
                        .HasColumnType("bit");

                    b.Property<DateTimeOffset?>("ModifiedDate")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("ModifiedUser")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<string>("SerializedProperties")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasColumnType("nvarchar(max)")
                        .HasDefaultValue("'{}'");

                    b.HasKey("Id");

                    b.HasIndex("Code")
                        .IsUnique();

                    SqlServerIndexBuilderExtensions.IncludeProperties(b.HasIndex("Code"), new[] { "Name" });

                    b.HasIndex("CreatedUser")
                        .HasFilter("[CreatedUser] is not null");

                    SqlServerIndexBuilderExtensions.IncludeProperties(b.HasIndex("CreatedUser"), new[] { "Name", "Code", "CreatedDate" });

                    b.ToTable("Content", "Contents");
                });
#pragma warning restore 612, 618
        }
    }
}
