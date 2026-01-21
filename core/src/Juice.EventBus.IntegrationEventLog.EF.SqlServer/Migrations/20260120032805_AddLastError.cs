using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.EventBus.IntegrationEventLog.EF.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddLastError : Migration
    {
        private readonly ISchemaDbContext _schema;
        public AddLastError()
        {
        }
        public AddLastError(ISchemaDbContext schema)
        {
            _schema = schema;
        }
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastError",
                schema: _schema.Schema,
                table: "IntegrationEventLog",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastError",
                schema: _schema.Schema,
                table: "IntegrationEventLog");
        }
    }
}
