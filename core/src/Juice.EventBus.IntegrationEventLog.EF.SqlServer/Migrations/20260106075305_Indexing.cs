using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.EventBus.IntegrationEventLog.EF.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class Indexing : Migration
    {
        private readonly ISchemaDbContext _schema;
        public Indexing()
        {

        }
        public Indexing(ISchemaDbContext schema)
        {
            _schema = schema;
        }
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "EventTypeName",
                schema: _schema.Schema,
                table: "IntegrationEventLog",
                type: "nvarchar(256)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "TransactionId",
                schema: _schema.Schema,
                table: "IntegrationEventLog",
                type: "nvarchar(64)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEventLog_TransactionId",
                schema: _schema.Schema,
                table: "IntegrationEventLog",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEventLog_Recovery",
                schema: _schema.Schema,
                table: "IntegrationEventLog",
                columns: new[] { "State", "ModificationTime", "TimesSent" },
                filter: "[ModificationTime] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IntegrationEventLog_TransactionId",
                schema: _schema.Schema,
                table: "IntegrationEventLog");

            migrationBuilder.DropIndex(
                name: "IX_Outbox_Recovery",
                schema: _schema.Schema,
                table: "IntegrationEventLog");

            migrationBuilder.AlterColumn<string>(
                name: "TransactionId",
                schema: _schema.Schema,
                table: "IntegrationEventLog",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)");

            migrationBuilder.AlterColumn<string>(
                name: "EventTypeName",
                schema: _schema.Schema,
                table: "IntegrationEventLog",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)");
        }
    }
}
