using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nobetci.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaveTypeCodeEn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodeEn",
                table: "LeaveTypes",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CodeEn",
                table: "LeaveTypes");
        }
    }
}
