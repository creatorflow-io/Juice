using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.Workflows.EF.SqlServer.Migrations
{
    public partial class AddEventAggregate : Migration
    {
        private string _schema;

        public AddEventAggregate() { }

        public AddEventAggregate(ISchemaDbContext schema)
        {
            _schema = schema.Schema;
        }
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventRecord",
                 schema: _schema,
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NodeId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IsStartEvent = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventRecord", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                 schema: _schema,
                name: "EventRecord");
        }
    }
}
