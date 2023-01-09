using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.Workflows.EF.PostgreSQL.PersistMigrations
{
    public partial class AddWfState : Migration
    {
        private string _schema;

        public AddWfState() { }

        public AddWfState(ISchemaDbContext schema)
        {
            _schema = schema.Schema;
        }

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkflowState",
                schema: _schema,
                columns: table => new
                {
                    WorkflowId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LastMessages = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Input = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Output = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    NodeStates = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowState", x => x.WorkflowId);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_FlowSnapshot_WorkflowState_WorkflowId",
                table: "FlowSnapshot",
                schema: _schema,
                column: "WorkflowId",
                principalTable: "WorkflowState",
                principalSchema: _schema,
                principalColumn: "WorkflowId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NodeSnapshot_WorkflowState_WorkflowId",
                table: "NodeSnapshot",
                schema: _schema,
                column: "WorkflowId",
                principalTable: "WorkflowState",
                principalSchema: _schema,
                principalColumn: "WorkflowId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessSnapshot_WorkflowState_WorkflowId",
                table: "ProcessSnapshot",
                schema: _schema,
                column: "WorkflowId",
                principalTable: "WorkflowState",
                principalSchema: _schema,
                principalColumn: "WorkflowId",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FlowSnapshot_WorkflowState_WorkflowId",
                schema: _schema,
                table: "FlowSnapshot");

            migrationBuilder.DropForeignKey(
                name: "FK_NodeSnapshot_WorkflowState_WorkflowId",
                schema: _schema,
                table: "NodeSnapshot");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessSnapshot_WorkflowState_WorkflowId",
                schema: _schema,
                table: "ProcessSnapshot");

            migrationBuilder.DropTable(
                schema: _schema,
                name: "WorkflowState");
        }
    }
}
