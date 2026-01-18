using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nobetci.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedPayroll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SavedPayrolls",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    DataSource = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NightStartHour = table.Column<int>(type: "integer", nullable: false),
                    NightEndHour = table.Column<int>(type: "integer", nullable: false),
                    PayrollDataJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedPayrolls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedPayrolls_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SavedPayrolls_OrganizationId",
                table: "SavedPayrolls",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedPayrolls_OrganizationId_Year_Month",
                table: "SavedPayrolls",
                columns: new[] { "OrganizationId", "Year", "Month" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavedPayrolls");
        }
    }
}
