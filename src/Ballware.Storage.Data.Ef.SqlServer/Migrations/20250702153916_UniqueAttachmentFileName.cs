using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ballware.Storage.Data.Ef.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class UniqueAttachmentFileName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "file_name",
                table: "attachment",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "entity",
                table: "attachment",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_attachment_tenant_id_entity_owner_id_file_name",
                table: "attachment",
                columns: new[] { "tenant_id", "entity", "owner_id", "file_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_attachment_tenant_id_entity_owner_id_file_name",
                table: "attachment");

            migrationBuilder.AlterColumn<string>(
                name: "file_name",
                table: "attachment",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "entity",
                table: "attachment",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
