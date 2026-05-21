using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.Messaging.Outbox.Migrations.PostgreSQL
{
    /// <inheritdoc />
    public partial class AddProcessedByToDelivery : Migration
    {
        private readonly ISchemaDbContext _schema;

        public AddProcessedByToDelivery() { }

        public AddProcessedByToDelivery(ISchemaDbContext schema)
        {
            _schema = schema;
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProcessedBy",
                schema: _schema.Schema,
                table: "OutboxDeliveries",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProcessedBy",
                schema: _schema.Schema,
                table: "OutboxDeliveries");
        }
    }
}
