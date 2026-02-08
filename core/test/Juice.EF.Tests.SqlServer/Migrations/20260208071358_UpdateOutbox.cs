using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.EF.Tests.SqlServer.Migrations
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

            migrationBuilder.AddColumn<string>(
                name: "Headers",
                schema: "Contents",
                table: "OutboxEvents",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<byte[]>(
                name: "PayloadBytes",
                schema: "Contents",
                table: "OutboxEvents",
                type: "varbinary(max)",
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

            migrationBuilder.AddColumn<string>(
                name: "Payload",
                schema: "Contents",
                table: "OutboxEvents",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
