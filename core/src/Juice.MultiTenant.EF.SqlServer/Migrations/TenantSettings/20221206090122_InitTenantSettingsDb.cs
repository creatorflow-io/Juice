using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.MultiTenant.EF.SqlServer.Migrations.TenantSettings
{
    public partial class InitTenantSettingsDb : Migration
    {
        private readonly string _schema = "App";

        public InitTenantSettingsDb() { }

        public InitTenantSettingsDb(ISchemaDbContext schema)
        {
            _schema = schema.Schema;
        }
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: _schema);

            migrationBuilder.CreateTable(
                name: "TenantSettings",
                schema: _schema,
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantSettings_Key_TenantId",
                schema: _schema,
                table: "TenantSettings",
                columns: new[] { "Key", "TenantId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantSettings",
                schema: _schema);
        }
    }
}
