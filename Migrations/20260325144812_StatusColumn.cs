using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KEYREGISTERAUTOMATION.Migrations
{
    /// <inheritdoc />
    public partial class StatusColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "AssignmentRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "AssignmentRecords");
        }
    }
}
