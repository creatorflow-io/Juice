using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.MediatR.RequestManager.EF.SqlServer.Migrations
{
    public partial class AddCompletedTime : Migration
    {
        private readonly string? _schema;

        public AddCompletedTime() { }

        public AddCompletedTime(ISchemaDbContext schema)
        {
            _schema = schema.Schema;
        }

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "Time",
                schema: _schema,
                table: "ClientRequest",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedTime",
                schema: _schema,
                table: "ClientRequest",
                type: "datetimeoffset",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedTime",
                schema: _schema,
                table: "ClientRequest");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "Time",
                schema: _schema,
                table: "ClientRequest",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldDefaultValueSql: "sysdatetimeoffset()");
        }
    }
}
