using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.EF.Tests.Migrations
{
    public partial class ChangePropertiesBehavior : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SerializedProperties",
                schema: "Contents",
                table: "Content");

            migrationBuilder.AddColumn<string>(
                name: "Properties",
                schema: "Contents",
                table: "Content",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{}");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Properties",
                schema: "Contents",
                table: "Content");

            migrationBuilder.AddColumn<string>(
                name: "SerializedProperties",
                schema: "Contents",
                table: "Content",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "'{}'");
        }
    }
}
