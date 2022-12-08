using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.MultiTenant.EF.PostgreSQL.Migrations.TenantStore
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
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldDefaultValue: new DateTimeOffset(new DateTime(2022, 12, 6, 15, 51, 39, 515, DateTimeKind.Unspecified).AddTicks(6336), new TimeSpan(0, 7, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "Properties",
                schema: _schema,
                table: "Tenant",
                type: "text",
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
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(2022, 12, 6, 15, 51, 39, 515, DateTimeKind.Unspecified).AddTicks(6336), new TimeSpan(0, 7, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "SerializedProperties",
                schema: _schema,
                table: "Tenant",
                type: "text",
                nullable: false,
                defaultValue: "'{}'");
        }
    }
}
