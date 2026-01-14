using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nobetci.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddHolidayHalfDayWorkHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HalfDayStartTime",
                table: "Holidays");

            migrationBuilder.AddColumn<decimal>(
                name: "HalfDayWorkHours",
                table: "Holidays",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HalfDayWorkHours",
                table: "Holidays");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "HalfDayStartTime",
                table: "Holidays",
                type: "time without time zone",
                nullable: true);
        }
    }
}
