using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ballware.Storage.Data.Ef.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attachment",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    uuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    creator_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    create_stamp = table.Column<DateTime>(type: "datetime2", nullable: true),
                    last_changer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    last_change_stamp = table.Column<DateTime>(type: "datetime2", nullable: true),
                    entity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    owner_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    file_name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    content_type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    storage_path = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attachment", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "temporary",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    uuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    creator_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    create_stamp = table.Column<DateTime>(type: "datetime2", nullable: true),
                    last_changer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    last_change_stamp = table.Column<DateTime>(type: "datetime2", nullable: true),
                    file_name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    content_type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    storage_path = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    expiry_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_temporary", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_attachment_tenant_id",
                table: "attachment",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_attachment_tenant_id_uuid",
                table: "attachment",
                columns: new[] { "tenant_id", "uuid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_temporary_tenant_id",
                table: "temporary",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_temporary_tenant_id_uuid",
                table: "temporary",
                columns: new[] { "tenant_id", "uuid" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attachment");

            migrationBuilder.DropTable(
                name: "temporary");
        }
    }
}
