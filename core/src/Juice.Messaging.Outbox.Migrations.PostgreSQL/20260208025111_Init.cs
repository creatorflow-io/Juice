using System;
using System.Collections.Generic;
using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.Messaging.Outbox.Migrations.PostgreSQL
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
                name: "OutboxEvents",
                schema: _schema.Schema,
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventTypeName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreationTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PayloadBytes = table.Column<byte[]>(type: "bytea", nullable: false),
                    Headers = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: false, defaultValue: new Dictionary<string, object>()),
                    TransactionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxEvents", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "OutboxDeliveries",
                schema: _schema.Schema,
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
                        principalSchema: _schema.Schema,
                        principalTable: "OutboxEvents",
                        principalColumn: "EventId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxDeliveries_EventId",
                schema: _schema.Schema,
                table: "OutboxDeliveries",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxDeliveries_Pending",
                schema: _schema.Schema,
                table: "OutboxDeliveries",
                column: "CreationTime",
                filter: "\"State\" = 0")
                .Annotation("Npgsql:IndexInclude", new[] { "EventId", "PublisherKey" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxDeliveries_PublisherKey",
                schema: _schema.Schema,
                table: "OutboxDeliveries",
                column: "PublisherKey");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxDeliveries_Recovery",
                schema: _schema.Schema,
                table: "OutboxDeliveries",
                column: "ProcessedOn",
                filter: "\"State\" = 1")
                .Annotation("Npgsql:IndexInclude", new[] { "EventId", "PublisherKey" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxDeliveries_Retry",
                schema: _schema.Schema,
                table: "OutboxDeliveries",
                column: "NextAttemptOn",
                filter: "\"State\" = 3 AND \"NextAttemptOn\" IS NOT NULL")
                .Annotation("Npgsql:IndexInclude", new[] { "EventId", "PublisherKey" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxEvents_TransactionId",
                schema: _schema.Schema,
                table: "OutboxEvents",
                column: "TransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxDeliveries",
                schema: _schema.Schema);

            migrationBuilder.DropTable(
                name: "OutboxEvents",
                schema: _schema.Schema);
        }
    }
}
