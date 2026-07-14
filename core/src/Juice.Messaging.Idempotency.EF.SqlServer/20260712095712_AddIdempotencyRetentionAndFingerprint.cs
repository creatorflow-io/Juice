using System;
using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.Messaging.Idempotency.EF.SqlServer
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
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: _schema?.Schema,
                table: "IdempotencyRecords",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldDefaultValue: new DateTimeOffset(new DateTime(2026, 2, 8, 8, 8, 1, 775, DateTimeKind.Unspecified).AddTicks(2151), new TimeSpan(0, 7, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAt",
                schema: _schema?.Schema,
                table: "IdempotencyRecords",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockedAt",
                schema: _schema?.Schema,
                table: "IdempotencyRecords",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                schema: _schema?.Schema,
                table: "IdempotencyRecords",
                type: "nvarchar(128)",
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

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: _schema?.Schema,
                table: "IdempotencyRecords",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(2026, 2, 8, 8, 8, 1, 775, DateTimeKind.Unspecified).AddTicks(2151), new TimeSpan(0, 7, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");
        }
    }
}
