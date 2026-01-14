using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nobetci.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultTemplatesInitialized : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DefaultTemplatesInitialized",
                table: "Organizations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultTemplatesInitialized",
                table: "Organizations");
        }
    }
}
