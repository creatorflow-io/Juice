using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.EventBus.IntegrationEventLog.EF.PostgreSQL.Migrations
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
                name: "TransactionId",
                schema: _schema.Schema,
                table: "IntegrationEventLog",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "EventTypeName",
                schema: _schema.Schema,
                table: "IntegrationEventLog",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEventLog_Recovery",
                schema: _schema.Schema,
                table: "IntegrationEventLog",
                columns: new[] { "State", "ModificationTime", "TimesSent" },
                filter: "\"ModificationTime\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEventLog_TransactionId",
                schema: _schema.Schema,
                table: "IntegrationEventLog",
                column: "TransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IntegrationEventLog_Recovery",
                schema: _schema.Schema,
                table: "IntegrationEventLog");

            migrationBuilder.DropIndex(
                name: "IX_IntegrationEventLog_TransactionId",
                schema: _schema.Schema,
                table: "IntegrationEventLog");

            migrationBuilder.AlterColumn<string>(
                name: "TransactionId",
                schema: _schema.Schema,
                table: "IntegrationEventLog",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "EventTypeName",
                schema: _schema.Schema,
                table: "IntegrationEventLog",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);
        }
    }
}
