using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.MultiTenant.EF.PostgreSQL.Migrations.TenantStore
{
    /// <inheritdoc />
    public partial class AddOwnerAndStatus : Migration
    {
        private readonly string _schema = "App";

        public AddOwnerAndStatus() { }

        public AddOwnerAndStatus(ISchemaDbContext schema)
        {
            _schema = schema.Schema;
        }
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: _schema,
                table: "Tenant",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ConnectionString",
                schema: _schema,
                table: "Tenant",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerUser",
                schema: _schema,
                table: "Tenant",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: _schema,
                table: "Tenant",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OwnerUser",
                schema: _schema,
                table: "Tenant");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: _schema,
                table: "Tenant");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: _schema,
                table: "Tenant",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "ConnectionString",
                schema: _schema,
                table: "Tenant",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}
