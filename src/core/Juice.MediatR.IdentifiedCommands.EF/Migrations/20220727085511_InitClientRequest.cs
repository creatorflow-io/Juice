using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.MediatR.IdentifiedCommands.EF.Migrations
{
    public partial class InitClientRequest : Migration
    {
        private readonly string _schema = "dbo";

        public InitClientRequest() { }

        public InitClientRequest(IDbContextSchema schema)
        {
            _schema = schema.Schema;
        }

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: _schema);

            migrationBuilder.CreateTable(
                name: "ClientRequest",
                schema: _schema,
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Time = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientRequest", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientRequest",
                schema: _schema);
        }
    }
}
