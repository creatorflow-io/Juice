using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.Workflows.EF.SqlServer.Migrations
{
    public partial class AddEventIsCompleted : Migration
    {
        private string _schema;

        public AddEventIsCompleted() { }

        public AddEventIsCompleted(ISchemaDbContext schema)
        {
            _schema = schema.Schema;
        }
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCompleted",
                schema: _schema,
                table: "EventRecord",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCompleted",
                schema: _schema,
                table: "EventRecord");
        }
    }
}
