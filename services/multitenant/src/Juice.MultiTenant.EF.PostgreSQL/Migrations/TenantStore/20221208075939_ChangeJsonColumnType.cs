using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.MultiTenant.EF.PostgreSQL.Migrations.TenantStore
{
    public partial class ChangeJsonColumnType : Migration
    {
        private readonly string _schema = "App";

        public ChangeJsonColumnType() { }

        public ChangeJsonColumnType(ISchemaDbContext schema)
        {
            _schema = schema.Schema;
        }

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Properties",
                schema: _schema,
                table: "Tenant");

            migrationBuilder.AddColumn<string>(
                name: "Properties",
                schema: _schema,
                table: "Tenant",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Properties",
                schema: _schema,
                table: "Tenant");

            migrationBuilder.AddColumn<string>(
                name: "Properties",
                schema: _schema,
                table: "Tenant",
                type: "text",
                nullable: false,
                defaultValue: "{}");
        }
    }
}
