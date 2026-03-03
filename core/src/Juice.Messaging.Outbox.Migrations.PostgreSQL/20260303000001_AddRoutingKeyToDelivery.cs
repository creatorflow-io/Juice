using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.Messaging.Outbox.Migrations.PostgreSQL
{
    /// <inheritdoc />
    public partial class AddRoutingKeyToDelivery : Migration
    {
        private readonly ISchemaDbContext _schema;

        public AddRoutingKeyToDelivery() { }

        public AddRoutingKeyToDelivery(ISchemaDbContext schema)
        {
            _schema = schema;
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RoutingKey",
                schema: _schema.Schema,
                table: "OutboxDeliveries",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RoutingKey",
                schema: _schema.Schema,
                table: "OutboxDeliveries");
        }
    }
}
