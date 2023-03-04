using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.Workflows.EF.PostgreSQL.Migrations
{
    public partial class AddEventCatchingKey : Migration
    {
        private string _schema;

        public AddEventCatchingKey() { }

        public AddEventCatchingKey(ISchemaDbContext schema)
        {
            _schema = schema.Schema;
        }

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CatchingKey",
                table: "EventRecord",
                schema: _schema,
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRecord_CorrelationId",
                table: "WorkflowRecord",
                schema: _schema,
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_EventRecord_CorrelationId_CatchingKey",
                table: "EventRecord",
                schema: _schema,
                columns: new[] { "CorrelationId", "CatchingKey" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowRecord_CorrelationId",
                schema: _schema,
                table: "WorkflowRecord");

            migrationBuilder.DropIndex(
                name: "IX_EventRecord_CorrelationId_CatchingKey",
                schema: _schema,
                table: "EventRecord");

            migrationBuilder.DropColumn(
                name: "CatchingKey",
                schema: _schema,
                table: "EventRecord");
        }
    }
}
