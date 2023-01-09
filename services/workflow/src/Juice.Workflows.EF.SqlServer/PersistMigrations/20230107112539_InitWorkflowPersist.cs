using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.Workflows.EF.SqlServer.PersistMigrations
{
    public partial class InitWorkflowPersist : Migration
    {
        private string _schema;

        public InitWorkflowPersist() { }

        public InitWorkflowPersist(ISchemaDbContext schema)
        {
            _schema = schema.Schema;
        }

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (_schema != null)
            {
                migrationBuilder.EnsureSchema(_schema);
            }
            migrationBuilder.CreateTable(
                name: "FlowSnapshot",
                schema: _schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    WorkflowId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowSnapshot", x => new { x.Id, x.WorkflowId });
                });

            migrationBuilder.CreateTable(
                name: "NodeSnapshot",
                schema: _schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    WorkflowId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Outcomes = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    User = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NodeSnapshot", x => new { x.Id, x.WorkflowId });
                });

            migrationBuilder.CreateTable(
                name: "ProcessSnapshot",
                schema: _schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    WorkflowId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessSnapshot", x => new { x.Id, x.WorkflowId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_FlowSnapshot_WorkflowId",
                table: "FlowSnapshot",
                schema: _schema,
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_NodeSnapshot_WorkflowId",
                table: "NodeSnapshot",
                schema: _schema,
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessSnapshot_WorkflowId",
                table: "ProcessSnapshot",
                schema: _schema,
                column: "WorkflowId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                schema: _schema,
                name: "FlowSnapshot");

            migrationBuilder.DropTable(
                schema: _schema,
                name: "NodeSnapshot");

            migrationBuilder.DropTable(
                schema: _schema,
                name: "ProcessSnapshot");
        }
    }
}
