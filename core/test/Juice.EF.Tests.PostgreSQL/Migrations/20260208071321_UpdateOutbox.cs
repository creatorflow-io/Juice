using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.EF.Tests.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class UpdateOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Payload",
                schema: "Contents",
                table: "OutboxEvents");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreationTime",
                schema: "Contents",
                table: "OutboxEvents",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AddColumn<Dictionary<string, object>>(
                name: "Headers",
                schema: "Contents",
                table: "OutboxEvents",
                type: "jsonb",
                nullable: false,
                defaultValue: new Dictionary<string, object>());

            migrationBuilder.AddColumn<byte[]>(
                name: "PayloadBytes",
                schema: "Contents",
                table: "OutboxEvents",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Headers",
                schema: "Contents",
                table: "OutboxEvents");

            migrationBuilder.DropColumn(
                name: "PayloadBytes",
                schema: "Contents",
                table: "OutboxEvents");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreationTime",
                schema: "Contents",
                table: "OutboxEvents",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "Payload",
                schema: "Contents",
                table: "OutboxEvents",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
