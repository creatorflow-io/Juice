using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.Audit.EF.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddExtraMetadataElapsedTime : Migration
    {
        private readonly string _schema = "App";

        public AddExtraMetadataElapsedTime()
        {
        }

        public AddExtraMetadataElapsedTime(ISchemaDbContext schema)
        {
            _schema = schema.Schema;
        }
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExtraMetadata",
                schema: _schema,
                table: "AccessLog",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<long>(
                name: "ResponseInfo_ElapsedMilliseconds",
                schema: _schema,
                table: "AccessLog",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtraMetadata",
                schema: _schema,
                table: "AccessLog");

            migrationBuilder.DropColumn(
                name: "ResponseInfo_ElapsedMilliseconds",
                schema: _schema,
                table: "AccessLog");
        }
    }
}
