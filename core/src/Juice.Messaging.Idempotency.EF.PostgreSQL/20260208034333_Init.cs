using System;
using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.Messaging.Idempotency.EF.PostgreSQL
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        private readonly ISchemaDbContext _schema;

        public Init() { }

        public Init(ISchemaDbContext schema)
        {
            _schema = schema;
        }
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (_schema.Schema != null)
            {
                migrationBuilder.EnsureSchema(
                    name: _schema.Schema);
            }

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                schema: _schema.Schema,
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Scope = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Result = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => new { x.Scope, x.Key });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdempotencyRecords",
                schema: _schema.Schema);
        }
    }
}
