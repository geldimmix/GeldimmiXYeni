using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nobetci.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQrMenuModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "UnitTypes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmployeeLimit",
                table: "Units",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "SystemSettings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DataType",
                table: "SystemSettings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "SystemSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Employees",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Employees",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanAccessCleaning",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanGroupCleaningSchedules",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanSelectCleaningFrequency",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "CleaningItemLimit",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CleaningQrAccessLimit",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CleaningScheduleLimit",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UnitEmployeeLimit",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UnitLimit",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ActivityLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    OrganizationId = table.Column<int>(type: "integer", nullable: true),
                    ActivityType = table.Column<int>(type: "integer", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EntityId = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Details = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityLogs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ActivityLogs_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BlogPosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TitleTr = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ExcerptTr = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ContentTr = table.Column<string>(type: "text", nullable: false),
                    KeywordsTr = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MetaDescriptionTr = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TitleEn = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ExcerptEn = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ContentEn = table.Column<string>(type: "text", nullable: true),
                    KeywordsEn = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MetaDescriptionEn = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OgImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CanonicalUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SchemaJson = table.Column<string>(type: "text", nullable: true),
                    RobotsMeta = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AuthorName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ViewCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlogPosts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CleaningScheduleGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CleaningScheduleGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CleaningScheduleGroups_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Modules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Icon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsPremium = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Modules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QrMenus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RestaurantName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RestaurantAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RestaurantPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    LogoUrl = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PrimaryColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    SecondaryColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    AcceptOrders = table.Column<bool>(type: "boolean", nullable: false),
                    Slug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SessionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QrMenus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QrMenus_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserApiCredentials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    ApiUsername = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ApiPasswordHash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MonthlyRequestLimit = table.Column<int>(type: "integer", nullable: false),
                    CurrentMonthRequests = table.Column<int>(type: "integer", nullable: false),
                    MonthlyResetDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalRequests = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserApiCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserApiCredentials_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserApiCredentials_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CleaningSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: true),
                    AccessCode = table.Column<string>(type: "text", nullable: true),
                    QrAccessCode = table.Column<string>(type: "text", nullable: false),
                    CleanerName = table.Column<string>(type: "text", nullable: true),
                    CleanerPhone = table.Column<string>(type: "text", nullable: true),
                    GroupId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CleaningSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CleaningSchedules_CleaningScheduleGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "CleaningScheduleGroups",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CleaningSchedules_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubModules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ModuleId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Icon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RouteUrl = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsPremium = table.Column<bool>(type: "boolean", nullable: false),
                    RequiredPermission = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubModules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubModules_Modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserModuleAccesses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ModuleId = table.Column<int>(type: "integer", nullable: false),
                    HasAccess = table.Column<bool>(type: "boolean", nullable: false),
                    AccessStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AccessEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserModuleAccesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserModuleAccesses_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserModuleAccesses_Modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QrMenuCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MenuId = table.Column<int>(type: "integer", nullable: false),
                    ParentCategoryId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Icon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QrMenuCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QrMenuCategories_QrMenuCategories_ParentCategoryId",
                        column: x => x.ParentCategoryId,
                        principalTable: "QrMenuCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QrMenuCategories_QrMenus_MenuId",
                        column: x => x.MenuId,
                        principalTable: "QrMenus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QrMenuTables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MenuId = table.Column<int>(type: "integer", nullable: false),
                    TableNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    QrCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QrMenuTables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QrMenuTables_QrMenus_MenuId",
                        column: x => x.MenuId,
                        principalTable: "QrMenus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CleaningItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScheduleId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Frequency = table.Column<int>(type: "integer", nullable: false),
                    FrequencyDays = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CleaningItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CleaningItems_CleaningSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "CleaningSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CleaningQrAccesses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScheduleId = table.Column<int>(type: "integer", nullable: false),
                    AccessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    MonthKey = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CleaningQrAccesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CleaningQrAccesses_CleaningSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "CleaningSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QrMenuItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DiscountedPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Calories = table.Column<int>(type: "integer", nullable: true),
                    PrepTimeMinutes = table.Column<int>(type: "integer", nullable: true),
                    Allergens = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Tags = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PortionSize = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    InStock = table.Column<bool>(type: "boolean", nullable: false),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false),
                    IsNew = table.Column<bool>(type: "boolean", nullable: false),
                    IsPopular = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QrMenuItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QrMenuItems_QrMenuCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "QrMenuCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QrMenuAccesses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MenuId = table.Column<int>(type: "integer", nullable: false),
                    TableId = table.Column<int>(type: "integer", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SessionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AccessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AccessDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QrMenuAccesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QrMenuAccesses_QrMenuTables_TableId",
                        column: x => x.TableId,
                        principalTable: "QrMenuTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_QrMenuAccesses_QrMenus_MenuId",
                        column: x => x.MenuId,
                        principalTable: "QrMenus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QrMenuOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MenuId = table.Column<int>(type: "integer", nullable: false),
                    TableId = table.Column<int>(type: "integer", nullable: true),
                    OrderNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CustomerPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    OrderedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QrMenuOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QrMenuOrders_QrMenuTables_TableId",
                        column: x => x.TableId,
                        principalTable: "QrMenuTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_QrMenuOrders_QrMenus_MenuId",
                        column: x => x.MenuId,
                        principalTable: "QrMenus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CleaningRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedByName = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedById = table.Column<string>(type: "text", nullable: true),
                    Note = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CleaningRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CleaningRecords_AspNetUsers_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CleaningRecords_CleaningItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "CleaningItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QrMenuOrderItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    MenuItemId = table.Column<int>(type: "integer", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QrMenuOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QrMenuOrderItems_QrMenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "QrMenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QrMenuOrderItems_QrMenuOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "QrMenuOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_ActivityType",
                table: "ActivityLogs",
                column: "ActivityType");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_CreatedAt",
                table: "ActivityLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_EntityType_EntityId",
                table: "ActivityLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_OrganizationId",
                table: "ActivityLogs",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_UserId",
                table: "ActivityLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_IsFeatured",
                table: "BlogPosts",
                column: "IsFeatured");

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_IsPublished",
                table: "BlogPosts",
                column: "IsPublished");

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_PublishedAt",
                table: "BlogPosts",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_Slug",
                table: "BlogPosts",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CleaningItems_ScheduleId",
                table: "CleaningItems",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_CleaningQrAccesses_ScheduleId",
                table: "CleaningQrAccesses",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_CleaningRecords_ItemId",
                table: "CleaningRecords",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CleaningRecords_ReviewedById",
                table: "CleaningRecords",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_CleaningScheduleGroups_OrganizationId",
                table: "CleaningScheduleGroups",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_CleaningSchedules_GroupId",
                table: "CleaningSchedules",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_CleaningSchedules_OrganizationId",
                table: "CleaningSchedules",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Modules_Code",
                table: "Modules",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QrMenuAccesses_AccessDate",
                table: "QrMenuAccesses",
                column: "AccessDate");

            migrationBuilder.CreateIndex(
                name: "IX_QrMenuAccesses_MenuId_AccessDate",
                table: "QrMenuAccesses",
                columns: new[] { "MenuId", "AccessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_QrMenuAccesses_TableId",
                table: "QrMenuAccesses",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "IX_QrMenuCategories_MenuId_DisplayOrder",
                table: "QrMenuCategories",
                columns: new[] { "MenuId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_QrMenuCategories_ParentCategoryId",
                table: "QrMenuCategories",
                column: "ParentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_QrMenuItems_CategoryId_DisplayOrder",
                table: "QrMenuItems",
                columns: new[] { "CategoryId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_QrMenuItems_IsActive",
                table: "QrMenuItems",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_QrMenuOrderItems_MenuItemId",
                table: "QrMenuOrderItems",
                column: "MenuItemId");

            migrationBuilder.CreateIndex(
                name: "IX_QrMenuOrderItems_OrderId",
                table: "QrMenuOrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_QrMenuOrders_MenuId_OrderedAt",
                table: "QrMenuOrders",
                columns: new[] { "MenuId", "OrderedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QrMenuOrders_OrderedAt",
                table: "QrMenuOrders",
                column: "OrderedAt");

            migrationBuilder.CreateIndex(
                name: "IX_QrMenuOrders_OrderNumber",
                table: "QrMenuOrders",
                column: "OrderNumber");

            migrationBuilder.CreateIndex(
                name: "IX_QrMenuOrders_Status",
                table: "QrMenuOrders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_QrMenuOrders_TableId",
                table: "QrMenuOrders",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "IX_QrMenus_IsActive",
                table: "QrMenus",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_QrMenus_SessionId",
                table: "QrMenus",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_QrMenus_Slug",
                table: "QrMenus",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QrMenus_UserId",
                table: "QrMenus",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_QrMenuTables_IsActive",
                table: "QrMenuTables",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_QrMenuTables_MenuId_QrCode",
                table: "QrMenuTables",
                columns: new[] { "MenuId", "QrCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubModules_ModuleId_Code",
                table: "SubModules",
                columns: new[] { "ModuleId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserApiCredentials_ApiUsername",
                table: "UserApiCredentials",
                column: "ApiUsername",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserApiCredentials_OrganizationId",
                table: "UserApiCredentials",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_UserApiCredentials_UserId_OrganizationId",
                table: "UserApiCredentials",
                columns: new[] { "UserId", "OrganizationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserModuleAccesses_ModuleId",
                table: "UserModuleAccesses",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserModuleAccesses_UserId_ModuleId",
                table: "UserModuleAccesses",
                columns: new[] { "UserId", "ModuleId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityLogs");

            migrationBuilder.DropTable(
                name: "BlogPosts");

            migrationBuilder.DropTable(
                name: "CleaningQrAccesses");

            migrationBuilder.DropTable(
                name: "CleaningRecords");

            migrationBuilder.DropTable(
                name: "QrMenuAccesses");

            migrationBuilder.DropTable(
                name: "QrMenuOrderItems");

            migrationBuilder.DropTable(
                name: "SubModules");

            migrationBuilder.DropTable(
                name: "UserApiCredentials");

            migrationBuilder.DropTable(
                name: "UserModuleAccesses");

            migrationBuilder.DropTable(
                name: "CleaningItems");

            migrationBuilder.DropTable(
                name: "QrMenuItems");

            migrationBuilder.DropTable(
                name: "QrMenuOrders");

            migrationBuilder.DropTable(
                name: "Modules");

            migrationBuilder.DropTable(
                name: "CleaningSchedules");

            migrationBuilder.DropTable(
                name: "QrMenuCategories");

            migrationBuilder.DropTable(
                name: "QrMenuTables");

            migrationBuilder.DropTable(
                name: "CleaningScheduleGroups");

            migrationBuilder.DropTable(
                name: "QrMenus");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "UnitTypes");

            migrationBuilder.DropColumn(
                name: "EmployeeLimit",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "DataType",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "CanAccessCleaning",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CanGroupCleaningSchedules",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CanSelectCleaningFrequency",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CleaningItemLimit",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CleaningQrAccessLimit",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CleaningScheduleLimit",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "UnitEmployeeLimit",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "UnitLimit",
                table: "AspNetUsers");
        }
    }
}
