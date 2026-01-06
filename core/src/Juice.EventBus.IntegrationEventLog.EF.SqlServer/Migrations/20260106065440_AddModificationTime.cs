using System;
using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.EventBus.IntegrationEventLog.EF.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddModificationTime : Migration
    {
        private readonly ISchemaDbContext _schema;
        public AddModificationTime()
        {

        }
        public AddModificationTime(ISchemaDbContext schema)
        {
            _schema = schema;
        }
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ModificationTime",
                schema: _schema.Schema,
                table: "IntegrationEventLog",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModificationTime",
                schema: _schema.Schema,
                table: "IntegrationEventLog");

        }
    }
}
