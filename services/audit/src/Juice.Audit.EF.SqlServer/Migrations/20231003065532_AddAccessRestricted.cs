using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.Audit.EF.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessRestricted : Migration
    {
        private readonly string _schema = "App";
        public AddAccessRestricted()
        {
        }

        public AddAccessRestricted(ISchemaDbContext schema)
        {
            _schema = schema.Schema;
        }
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRestricted",
                schema: _schema,
                table: "AccessLog",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_DataAudit_Kvps",
                schema: _schema,
                table: "DataAudit",
                column: "Kvps");

            migrationBuilder.CreateIndex(
                name: "IX_AccessLog_IsRestricted",
                schema: _schema,
                table: "AccessLog",
                column: "IsRestricted");

            migrationBuilder.CreateIndex(
                name: "IX_AccessLog_Req_Path",
                schema: _schema,
                table: "AccessLog",
                column: "Req_Path");

            migrationBuilder.CreateIndex(
                name: "IX_AccessLog_Req_RIPA",
                schema: _schema,
                table: "AccessLog",
                column: "Req_RIPA");

            migrationBuilder.CreateIndex(
                name: "IX_AccessLog_Res_Status",
                schema: _schema,
                table: "AccessLog",
                column: "Res_Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DataAudit_Kvps",
                schema: _schema,
                table: "DataAudit");

            migrationBuilder.DropIndex(
                name: "IX_AccessLog_IsRestricted",
                schema: _schema,
                table: "AccessLog");

            migrationBuilder.DropIndex(
                name: "IX_AccessLog_Req_Path",
                schema: _schema,
                table: "AccessLog");

            migrationBuilder.DropIndex(
                name: "IX_AccessLog_Req_RIPA",
                schema: _schema,
                table: "AccessLog");

            migrationBuilder.DropIndex(
                name: "IX_AccessLog_Res_Status",
                schema: _schema,
                table: "AccessLog");

            migrationBuilder.DropColumn(
                name: "IsRestricted",
                schema: _schema,
                table: "AccessLog");
        }
    }
}
