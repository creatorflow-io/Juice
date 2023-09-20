using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.Audit.EF.SqlServer.Migrations
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    User = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValueSql: "'{}'"),
                    Req_Method = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Req_TraceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Req_Host = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Req_Path = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Req_Data = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Req_Query = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Req_Headers = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Req_Scheme = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Req_RIPA = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Req_Zone = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Srv_Machine = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Srv_OS = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Srv_AppVer = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Srv_App = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Res_Status = table.Column<int>(type: "int", nullable: true),
                    Res_Data = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Res_Msg = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Res_Err = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Res_Headers = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Res_ElapsedMs = table.Column<long>(type: "bigint", nullable: true)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    User = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Db = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Schema = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Tbl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Kvps = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, defaultValueSql: "'{}'"),
                    Changes = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValueSql: "'{}'"),
                    TraceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
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
                name: "IX_AccessLog_Req_TraceId",
                table: "AccessLog",
                column: "Req_TraceId");

            migrationBuilder.CreateIndex(
                schema: _schema,
                name: "IX_AccessLog_User_Action",
                table: "AccessLog",
                columns: new[] { "User", "Action" });

            migrationBuilder.CreateIndex(
                schema: _schema,
                name: "IX_DataAudit_DateTime",
                table: "DataAudit",
                column: "DateTime");

            migrationBuilder.CreateIndex(
                schema: _schema,
                name: "IX_DataAudit_Db_Schema_Tbl",
                table: "DataAudit",
                columns: new[] { "Db", "Schema", "Tbl" });

            migrationBuilder.CreateIndex(
                schema: _schema,
                name: "IX_DataAudit_TraceId",
                table: "DataAudit",
                column: "TraceId");

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
