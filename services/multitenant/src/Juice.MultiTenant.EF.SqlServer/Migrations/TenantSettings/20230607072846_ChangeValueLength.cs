using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.MultiTenant.EF.SqlServer.Migrations.TenantSettings
{
    /// <inheritdoc />
    public partial class ChangeValueLength : Migration
    {
        private readonly string _schema = "App";

        public ChangeValueLength() { }

        public ChangeValueLength(ISchemaDbContext schema)
        {
            _schema = schema.Schema;
        }
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TenantSettings_Key_TenantId",
                schema: _schema,
                table: "TenantSettings");

            migrationBuilder.AlterColumn<string>(
                name: "Value",
                schema: _schema,
                table: "TenantSettings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: _schema,
                table: "TenantSettings",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.CreateIndex(
                name: "IX_TenantSettings_Key_TenantId",
                schema: _schema,
                table: "TenantSettings",
                columns: new[] { "Key", "TenantId" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TenantSettings_Key_TenantId",
                schema: _schema,
                table: "TenantSettings");

            migrationBuilder.AlterColumn<string>(
                name: "Value",
                schema: _schema,
                table: "TenantSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: _schema,
                table: "TenantSettings",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantSettings_Key_TenantId",
                schema: _schema,
                table: "TenantSettings",
                columns: new[] { "Key", "TenantId" },
                unique: true);
        }
    }
}
