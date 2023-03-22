using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.Workflows.EF.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class InitWorkflow : Migration
    {
        private string _schema;

        public InitWorkflow() { }

        public InitWorkflow(ISchemaDbContext schema)
        {
            _schema = schema.Schema;
        }
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            if (_schema != null)
            {
                migrationBuilder.EnsureSchema(_schema);
            }

            migrationBuilder.CreateTable(
                name: "EventRecord",
                schema: _schema,
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NodeId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CatchingKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsStartEvent = table.Column<bool>(type: "boolean", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastCall = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventRecord", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowDefinition",
                schema: _schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RawData = table.Column<string>(type: "text", nullable: true),
                    RawFormat = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Data = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Disabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedUser = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ModifiedUser = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowDefinition", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowRecord",
                schema: _schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DefinitionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    StatusLastUpdate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FaultMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Disabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowRecord", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventRecord_CorrelationId_CatchingKey",
                table: "EventRecord",
                schema: _schema,
                columns: new[] { "CorrelationId", "CatchingKey" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRecord_CorrelationId",
                table: "WorkflowRecord",
                schema: _schema,
                column: "CorrelationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                schema: _schema,
                name: "EventRecord");

            migrationBuilder.DropTable(
                schema: _schema,
                name: "WorkflowDefinition");

            migrationBuilder.DropTable(
                schema: _schema,
                name: "WorkflowRecord");
        }
    }
}
