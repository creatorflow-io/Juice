using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.EF.Tests.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddAutoUpdateAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AlternativeCreatedUser",
                schema: "Contents",
                table: "Content",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AlternativeCreationDate",
                schema: "Contents",
                table: "Content",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AlternativeModificationDate",
                schema: "Contents",
                table: "Content",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AlternativeModifiedUser",
                schema: "Contents",
                table: "Content",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlternativeCreatedUser",
                schema: "Contents",
                table: "Content");

            migrationBuilder.DropColumn(
                name: "AlternativeCreationDate",
                schema: "Contents",
                table: "Content");

            migrationBuilder.DropColumn(
                name: "AlternativeModificationDate",
                schema: "Contents",
                table: "Content");

            migrationBuilder.DropColumn(
                name: "AlternativeModifiedUser",
                schema: "Contents",
                table: "Content");
        }
    }
}
