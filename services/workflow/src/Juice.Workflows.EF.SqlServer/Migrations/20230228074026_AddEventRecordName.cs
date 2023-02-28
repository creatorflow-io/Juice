using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.Workflows.EF.SqlServer.Migrations
{
    public partial class AddEventRecordName : Migration
    {
        private string _schema;

        public AddEventRecordName() { }

        public AddEventRecordName(ISchemaDbContext schema)
        {
            _schema = schema.Schema;
        }
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "EventRecord",
                schema: _schema,
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayName",
                schema: _schema,
                table: "EventRecord");
        }
    }
}
