using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.EF.Tests.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxEvents_Recovery",
                schema: "Contents",
                table: "OutboxEvents");

            migrationBuilder.DropColumn(
                name: "LastError",
                schema: "Contents",
                table: "OutboxEvents");

            migrationBuilder.DropColumn(
                name: "ProcessedOn",
                schema: "Contents",
                table: "OutboxEvents");

            migrationBuilder.DropColumn(
                name: "State",
                schema: "Contents",
                table: "OutboxEvents");

            migrationBuilder.DropColumn(
                name: "TimesSent",
                schema: "Contents",
                table: "OutboxEvents");

            migrationBuilder.AlterColumn<string>(
                name: "TransactionId",
                schema: "Contents",
                table: "OutboxEvents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "Contents",
                table: "OutboxEvents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OutboxDeliveries",
                schema: "Contents",
                columns: table => new
                {
                    DeliveryId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublisherKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Destination = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreationTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    ProcessedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    NextAttemptOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxDeliveries", x => x.DeliveryId);
                    table.ForeignKey(
                        name: "FK_OutboxDeliveries_OutboxEvents_EventId",
                        column: x => x.EventId,
                        principalSchema: "Contents",
                        principalTable: "OutboxEvents",
                        principalColumn: "EventId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxDeliveries_EventId",
                schema: "Contents",
                table: "OutboxDeliveries",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxDeliveries_Pending",
                schema: "Contents",
                table: "OutboxDeliveries",
                column: "CreationTime",
                filter: "\"State\" = 0")
                .Annotation("Npgsql:IndexInclude", new[] { "EventId", "PublisherKey" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxDeliveries_PublisherKey",
                schema: "Contents",
                table: "OutboxDeliveries",
                column: "PublisherKey");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxDeliveries_Recovery",
                schema: "Contents",
                table: "OutboxDeliveries",
                column: "ProcessedOn",
                filter: "\"State\" = 1")
                .Annotation("Npgsql:IndexInclude", new[] { "EventId", "PublisherKey" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxDeliveries_Retry",
                schema: "Contents",
                table: "OutboxDeliveries",
                column: "NextAttemptOn",
                filter: "\"State\" = 3 AND \"NextAttemptOn\" IS NOT NULL")
                .Annotation("Npgsql:IndexInclude", new[] { "EventId", "PublisherKey" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxDeliveries",
                schema: "Contents");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Contents",
                table: "OutboxEvents");

            migrationBuilder.AlterColumn<string>(
                name: "TransactionId",
                schema: "Contents",
                table: "OutboxEvents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                schema: "Contents",
                table: "OutboxEvents",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessedOn",
                schema: "Contents",
                table: "OutboxEvents",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "State",
                schema: "Contents",
                table: "OutboxEvents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TimesSent",
                schema: "Contents",
                table: "OutboxEvents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxEvents_Recovery",
                schema: "Contents",
                table: "OutboxEvents",
                columns: new[] { "State", "ProcessedOn", "TimesSent" },
                filter: "[ProcessedOn] IS NOT NULL");
        }
    }
}
