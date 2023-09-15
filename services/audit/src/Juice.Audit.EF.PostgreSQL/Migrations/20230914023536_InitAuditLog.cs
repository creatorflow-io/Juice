using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.Audit.EF.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class InitAuditLog : Migration
    {
        private readonly string _schema = "App";

        public InitAuditLog()
        {
        }

        public InitAuditLog(ISchemaDbContext schema)
        {
            _schema = schema.Schema;
        }
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                schema: _schema,
                name: "AccessLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    User = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Action = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RequestInfo_Method = table.Column<string>(type: "text", nullable: true),
                    RequestInfo_RequestId = table.Column<string>(type: "text", nullable: true),
                    RequestInfo_Host = table.Column<string>(type: "text", nullable: true),
                    RequestInfo_Path = table.Column<string>(type: "text", nullable: true),
                    RequestInfo_Data = table.Column<string>(type: "text", nullable: true),
                    RequestInfo_QueryString = table.Column<string>(type: "text", nullable: true),
                    RequestInfo_Headers = table.Column<string>(type: "text", nullable: true),
                    RequestInfo_Schema = table.Column<string>(type: "text", nullable: true),
                    RequestInfo_RemoteIpAddress = table.Column<string>(type: "text", nullable: true),
                    RequestInfo_AccessZone = table.Column<string>(type: "text", nullable: true),
                    ServerInfo_MachineName = table.Column<string>(type: "text", nullable: true),
                    ServerInfo_OSVersion = table.Column<string>(type: "text", nullable: true),
                    ServerInfo_SoftwareVersion = table.Column<string>(type: "text", nullable: true),
                    ServerInfo_AppName = table.Column<string>(type: "text", nullable: true),
                    ResponseInfo_StatusCode = table.Column<int>(type: "integer", nullable: true),
                    ResponseInfo_Data = table.Column<string>(type: "text", nullable: true),
                    ResponseInfo_Message = table.Column<string>(type: "text", nullable: true),
                    ResponseInfo_Error = table.Column<string>(type: "text", nullable: true),
                    ResponseInfo_Headers = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                schema: _schema,
                name: "DataAudit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    User = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Action = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Database = table.Column<string>(type: "text", nullable: false),
                    Schema = table.Column<string>(type: "text", nullable: false),
                    Table = table.Column<string>(type: "text", nullable: false),
                    DataChanges = table.Column<string>(type: "text", nullable: false),
                    AccessId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataAudit", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                schema: _schema,
                name: "IX_AccessLog_DateTime",
                table: "AccessLog",
                column: "DateTime");

            migrationBuilder.CreateIndex(
                schema: _schema,
                name: "IX_AccessLog_User_Action",
                table: "AccessLog",
                columns: new[] { "User", "Action" });

            migrationBuilder.CreateIndex(
                schema: _schema,
                name: "IX_DataAudit_AccessId",
                table: "DataAudit",
                column: "AccessId");

            migrationBuilder.CreateIndex(
                schema: _schema,
                name: "IX_DataAudit_Database_Schema_Table",
                table: "DataAudit",
                columns: new[] { "Database", "Schema", "Table" });

            migrationBuilder.CreateIndex(
                schema: _schema,
                name: "IX_DataAudit_DateTime",
                table: "DataAudit",
                column: "DateTime");

            migrationBuilder.CreateIndex(
                schema: _schema,
                name: "IX_DataAudit_User_Action",
                table: "DataAudit",
                columns: new[] { "User", "Action" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                schema: _schema,
                name: "AccessLog");

            migrationBuilder.DropTable(
                schema: _schema,
                name: "DataAudit");
        }
    }
}
