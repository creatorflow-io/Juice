using System;
using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.MediatR.RequestManager.EF.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddResultCache : Migration
    {
        private readonly ISchemaDbContext _schema;
        public AddResultCache() { }

        public AddResultCache(ISchemaDbContext schema)
        {
            _schema = schema;
        }
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Result",
                schema: _schema.Schema,
                table: "ClientRequest",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Result",
                schema: _schema.Schema,
                table: "ClientRequest");
        }
    }
}
