using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nobetci.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeWeekendSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "DailyWorkHours",
                table: "Employees",
                type: "numeric",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SaturdayWorkHours",
                table: "Employees",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WeekendWorkMode",
                table: "Employees",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SaturdayWorkHours",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "WeekendWorkMode",
                table: "Employees");

            migrationBuilder.AlterColumn<decimal>(
                name: "DailyWorkHours",
                table: "Employees",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric");
        }
    }
}
