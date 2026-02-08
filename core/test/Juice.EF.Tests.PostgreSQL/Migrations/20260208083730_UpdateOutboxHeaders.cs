using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.EF.Tests.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class UpdateOutboxHeaders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Headers",
                schema: "Contents",
                table: "OutboxEvents",
                type: "text",
                nullable: false,
                defaultValue: "{}",
                oldClrType: typeof(Dictionary<string, object>),
                oldType: "jsonb",
                oldDefaultValue: new Dictionary<string, object>());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Dictionary<string, object>>(
                name: "Headers",
                schema: "Contents",
                table: "OutboxEvents",
                type: "jsonb",
                nullable: false,
                defaultValue: new Dictionary<string, object>(),
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "{}");
        }
    }
}
