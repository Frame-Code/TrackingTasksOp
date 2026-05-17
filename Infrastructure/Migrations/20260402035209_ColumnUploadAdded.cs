#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ColumnUploadAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OpenProjectId",
                table: "Tasks",
                newName: "WorkPackageId");

            migrationBuilder.AddColumn<bool>(
                name: "Uploaded",
                table: "TaskTimeDetails",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Uploaded",
                table: "TaskTimeDetails");

            migrationBuilder.RenameColumn(
                name: "WorkPackageId",
                table: "Tasks",
                newName: "OpenProjectId");
        }
    }
}
