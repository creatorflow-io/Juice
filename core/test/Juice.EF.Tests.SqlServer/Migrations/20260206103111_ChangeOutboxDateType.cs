using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.EF.Tests.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class ChangeOutboxDateType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreationTime",
                schema: "Contents",
                table: "OutboxEvents",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "CreationTime",
                schema: "Contents",
                table: "OutboxEvents",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");
        }
    }
}
