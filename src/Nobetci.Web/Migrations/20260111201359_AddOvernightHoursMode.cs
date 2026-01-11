using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nobetci.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddOvernightHoursMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OvernightHoursMode",
                table: "Shifts",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OvernightHoursMode",
                table: "Shifts");
        }
    }
}
