using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.MultiTenant.EF.PostgreSQL.Migrations.TenantStore
{
    public partial class InitTenantStoreDb : Migration
    {
        private readonly string _schema = "App";

        public InitTenantStoreDb() { }

        public InitTenantStoreDb(ISchemaDbContext schema)
        {
            _schema = schema.Schema;
        }
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: _schema);

            migrationBuilder.CreateTable(
                name: "Tenant",
                schema: _schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Identifier = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ConnectionString = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Disabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUser = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ModifiedUser = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValue: new DateTimeOffset(new DateTime(2022, 12, 6, 15, 51, 39, 515, DateTimeKind.Unspecified).AddTicks(6336), new TimeSpan(0, 7, 0, 0, 0))),
                    ModifiedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SerializedProperties = table.Column<string>(type: "text", nullable: false, defaultValue: "'{}'")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenant", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tenant_Identifier",
                schema: _schema,
                table: "Tenant",
                column: "Identifier",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tenant",
                schema: _schema);
        }
    }
}
