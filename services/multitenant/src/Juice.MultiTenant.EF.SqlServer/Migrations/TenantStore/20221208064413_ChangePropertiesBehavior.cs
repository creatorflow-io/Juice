using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.MultiTenant.EF.SqlServer.Migrations.TenantStore
{
    public partial class ChangePropertiesBehavior : Migration
    {
        private readonly string _schema = "App";

        public ChangePropertiesBehavior() { }

        public ChangePropertiesBehavior(ISchemaDbContext schema)
        {
            _schema = schema.Schema;
        }
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SerializedProperties",
                schema: _schema,
                table: "Tenant");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedDate",
                schema: _schema,
                table: "Tenant",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldDefaultValue: new DateTimeOffset(new DateTime(2022, 12, 6, 15, 42, 53, 318, DateTimeKind.Unspecified).AddTicks(4170), new TimeSpan(0, 7, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "Properties",
                schema: _schema,
                table: "Tenant",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{}");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Properties",
                schema: _schema,
                table: "Tenant");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedDate",
                schema: _schema,
                table: "Tenant",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(2022, 12, 6, 15, 42, 53, 318, DateTimeKind.Unspecified).AddTicks(4170), new TimeSpan(0, 7, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AddColumn<string>(
                name: "SerializedProperties",
                schema: _schema,
                table: "Tenant",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "'{}'");
        }
    }
}
