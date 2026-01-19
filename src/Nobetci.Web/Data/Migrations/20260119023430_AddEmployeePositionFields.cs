using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nobetci.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeePositionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcademicTitle",
                table: "Employees",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsNonHealthServices",
                table: "Employees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PositionType",
                table: "Employees",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShiftScore",
                table: "Employees",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcademicTitle",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "IsNonHealthServices",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PositionType",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ShiftScore",
                table: "Employees");
        }
    }
}
