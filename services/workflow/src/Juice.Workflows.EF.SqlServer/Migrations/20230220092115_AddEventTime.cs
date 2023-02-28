using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.Workflows.EF.SqlServer.Migrations
{
    public partial class AddEventTime : Migration
    {
        private string _schema;

        public AddEventTime() { }

        public AddEventTime(ISchemaDbContext schema)
        {
            _schema = schema.Schema;
        }
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                schema: _schema,
                name: "CreatedDate",
                table: "EventRecord",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                schema: _schema,
                name: "LastCall",
                table: "EventRecord",
                type: "datetimeoffset",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                schema: _schema,
                name: "CreatedDate",
                table: "EventRecord");

            migrationBuilder.DropColumn(
                schema: _schema,
                name: "LastCall",
                table: "EventRecord");
        }
    }
}
