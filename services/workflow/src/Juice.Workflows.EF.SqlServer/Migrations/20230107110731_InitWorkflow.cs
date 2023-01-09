using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.Workflows.EF.SqlServer.Migrations
{
    public partial class InitWorkflow : Migration
    {
        private string _schema;

        public InitWorkflow() { }

        public InitWorkflow(ISchemaDbContext schema)
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
                name: "WorkflowDefinition",
                schema: _schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RawData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RawFormat = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Data = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Disabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedUser = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedUser = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
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
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DefinitionId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    StatusLastUpdate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FaultMessage = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Disabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowRecord", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkflowDefinition",
                schema: _schema);

            migrationBuilder.DropTable(
                name: "WorkflowRecord",
                schema: _schema);
        }
    }
}
