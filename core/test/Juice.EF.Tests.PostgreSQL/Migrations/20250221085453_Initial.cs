using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.EF.Tests.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Contents");

            migrationBuilder.CreateTable(
                name: "Content",
                schema: "Contents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Properties = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Disabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUser = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ModifiedUser = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Content", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrossTenantContent",
                schema: "Contents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Disabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrossTenantContent", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Content_Code",
                schema: "Contents",
                table: "Content",
                column: "Code",
                unique: true)
                .Annotation("Npgsql:IndexInclude", new[] { "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Content_CreatedUser",
                schema: "Contents",
                table: "Content",
                column: "CreatedUser")
                .Annotation("Npgsql:IndexInclude", new[] { "Name", "Code", "CreatedDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Content",
                schema: "Contents");

            migrationBuilder.DropTable(
                name: "CrossTenantContent",
                schema: "Contents");
        }
    }
}
