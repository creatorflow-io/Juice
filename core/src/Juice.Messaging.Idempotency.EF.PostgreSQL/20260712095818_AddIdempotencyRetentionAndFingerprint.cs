using System;
using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.Messaging.Idempotency.EF.PostgreSQL
{
    /// <inheritdoc />
    public partial class AddIdempotencyRetentionAndFingerprint : Migration
    {
        private readonly ISchemaDbContext _schema;

        public AddIdempotencyRetentionAndFingerprint() { }

        public AddIdempotencyRetentionAndFingerprint(ISchemaDbContext schema)
        {
            _schema = schema;
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAt",
                schema: _schema?.Schema,
                table: "IdempotencyRecords",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockedAt",
                schema: _schema?.Schema,
                table: "IdempotencyRecords",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                schema: _schema?.Schema,
                table: "IdempotencyRecords",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Idempotency_ExpiresAt",
                schema: _schema?.Schema,
                table: "IdempotencyRecords",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_Idempotency_State_LockedAt",
                schema: _schema?.Schema,
                table: "IdempotencyRecords",
                columns: new[] { "State", "LockedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Idempotency_ExpiresAt",
                schema: _schema?.Schema,
                table: "IdempotencyRecords");

            migrationBuilder.DropIndex(
                name: "IX_Idempotency_State_LockedAt",
                schema: _schema?.Schema,
                table: "IdempotencyRecords");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                schema: _schema?.Schema,
                table: "IdempotencyRecords");

            migrationBuilder.DropColumn(
                name: "LockedAt",
                schema: _schema?.Schema,
                table: "IdempotencyRecords");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                schema: _schema?.Schema,
                table: "IdempotencyRecords");
        }
    }
}
