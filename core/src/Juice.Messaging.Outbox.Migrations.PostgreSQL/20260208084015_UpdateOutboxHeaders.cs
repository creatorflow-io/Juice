using System.Collections.Generic;
using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.Messaging.Outbox.Migrations.PostgreSQL
{
    /// <inheritdoc />
    public partial class UpdateOutboxHeaders : Migration
    {

        private readonly ISchemaDbContext _schema;

        public UpdateOutboxHeaders() { }

        public UpdateOutboxHeaders(ISchemaDbContext schema)
        {
            _schema = schema;
        }
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Headers",
                schema: _schema.Schema,
                table: "OutboxEvents",
                type: "text",
                nullable: false,
                defaultValue: "{}",
                oldClrType: typeof(Dictionary<string, object>),
                oldType: "jsonb",
                oldDefaultValue: new Dictionary<string, object>());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Dictionary<string, object>>(
                name: "Headers",
                schema: _schema.Schema,
                table: "OutboxEvents",
                type: "jsonb",
                nullable: false,
                defaultValue: new Dictionary<string, object>(),
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "{}");
        }
    }
}
