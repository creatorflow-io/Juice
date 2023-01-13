using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.Timers.EF.SqlServer.Migrations
{
    public partial class InitTimer : Migration
    {
        private string _schema;

        public InitTimer() { }

        public InitTimer(ISchemaDbContext schema)
        {
            _schema = schema.Schema;
        }

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (!string.IsNullOrEmpty(_schema))
            {
                migrationBuilder.EnsureSchema(
                    name: _schema);
            }

            migrationBuilder.CreateTable(
                name: "TimerRequest",
                schema: _schema,
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Issuer = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AbsoluteExpired = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    ProcessedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimerRequest", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TimerRequest_AbsoluteExpired_IsCompleted",
                schema: _schema,
                table: "TimerRequest",
                columns: new[] { "AbsoluteExpired", "IsCompleted" });

            migrationBuilder.CreateIndex(
                name: "IX_TimerRequest_CorrelationId",
                schema: _schema,
                table: "TimerRequest",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_TimerRequest_Issuer",
                schema: _schema,
                table: "TimerRequest",
                column: "Issuer");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TimerRequest",
                schema: _schema);
        }
    }
}
