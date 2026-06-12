using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.Messaging.Idempotency.EF.SqlServer
{
    /// <inheritdoc />
    public partial class AddProcessedBy : Migration
    {
        private readonly ISchemaDbContext _schema;

        public AddProcessedBy() { }

        public AddProcessedBy(ISchemaDbContext schema)
        {
            _schema = schema;
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProcessedBy",
                schema: _schema?.Schema,
                table: "IdempotencyRecords",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProcessedBy",
                schema: _schema?.Schema,
                table: "IdempotencyRecords");
        }
    }
}
