using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Nobetci.Web.Data;
using Nobetci.Web.Data.Entities;
using Nobetci.Web.Middleware;
using Nobetci.Web.Services;
using Resend;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    
    // User settings
    options.User.RequireUniqueEmail = true;
    
    // Sign in settings - require email confirmation
    options.SignIn.RequireConfirmedEmail = true;
    
    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Google Authentication (only if configured)
var googleClientId = builder.Configuration["Google:ClientId"];
var googleClientSecret = builder.Configuration["Google:ClientSecret"];

if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret) &&
    googleClientId != "YOUR_GOOGLE_CLIENT_ID" && googleClientSecret != "YOUR_GOOGLE_CLIENT_SECRET")
{
    builder.Services.AddAuthentication()
        .AddGoogle(options =>
        {
            options.ClientId = googleClientId;
            options.ClientSecret = googleClientSecret;
        });
}

// Cookie settings
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
});

// Localization
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("tr"),
        new CultureInfo("en")
    };
    
    options.DefaultRequestCulture = new RequestCulture("tr");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    
    // Add cookie provider for language selection
    options.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider
    {
        CookieName = ".Nobetci.Culture"
    });
});

// Resend Email Service
builder.Services.AddOptions();
builder.Services.AddHttpClient<ResendClient>();
builder.Services.Configure<ResendClientOptions>(options =>
{
    options.ApiToken = builder.Configuration["Resend:ApiToken"] 
        ?? Environment.GetEnvironmentVariable("RESEND_APITOKEN") 
        ?? throw new InvalidOperationException("Resend API token is not configured");
});
builder.Services.AddTransient<IResend, ResendClient>();

// Custom services
builder.Services.AddScoped<ITranslationService, TranslationService>();
builder.Services.AddHttpClient<ITranslationService, TranslationService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IVisitorLogService, VisitorLogService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<ISystemSettingsService, SystemSettingsService>();
builder.Services.AddScoped<IEmailSender, EmailService>();

// Session for guest users
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(7);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".Nobetci.Session";
});

// MVC
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Forward proxy headers (for production behind reverse proxy - nginx, Apache, etc.)
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | 
                       ForwardedHeaders.XForwardedProto | 
                       ForwardedHeaders.XForwardedHost,
    // Trust known proxy IPs (adjust for your production setup)
    RequireHeaderSymmetry = false,
    ForwardedProtoHeaderName = "X-Forwarded-Proto",
    ForwardedHostHeaderName = "X-Forwarded-Host"
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Localization middleware
app.UseRequestLocalization();

// Session
app.UseSession();

// Visitor tracking
app.UseVisitorTracking();

app.UseAuthentication();
app.UseAuthorization();

// Routes
app.MapControllerRoute(
    name: "localized",
    pattern: "{lang:regex(^(tr|en)$)}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Apply migrations and seed data on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        
        // Helper to safely execute SQL (catches and logs errors but continues)
        async Task SafeExecuteSql(string sql, string description)
        {
            try { await context.Database.ExecuteSqlRawAsync(sql); }
            catch (Exception ex) { Console.WriteLine($"SQL [{description}]: {ex.Message}"); }
        }
        
        // Apply pending column additions manually before migration
        await SafeExecuteSql(@"
            DO $$ 
            BEGIN 
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Employees' AND column_name='SaturdayWorkHours') THEN
                    ALTER TABLE ""Employees"" ADD COLUMN ""SaturdayWorkHours"" DECIMAL NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Employees' AND column_name='WeekendWorkMode') THEN
                    ALTER TABLE ""Employees"" ADD COLUMN ""WeekendWorkMode"" INTEGER DEFAULT 0 NOT NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Employees' AND column_name='PositionType') THEN
                    ALTER TABLE ""Employees"" ADD COLUMN ""PositionType"" VARCHAR(20) NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Employees' AND column_name='AcademicTitle') THEN
                    ALTER TABLE ""Employees"" ADD COLUMN ""AcademicTitle"" VARCHAR(50) NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Employees' AND column_name='ShiftScore') THEN
                    ALTER TABLE ""Employees"" ADD COLUMN ""ShiftScore"" INTEGER DEFAULT 100 NOT NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Employees' AND column_name='IsNonHealthServices') THEN
                    ALTER TABLE ""Employees"" ADD COLUMN ""IsNonHealthServices"" BOOLEAN DEFAULT FALSE NOT NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Employees' AND column_name='Email') THEN
                    ALTER TABLE ""Employees"" ADD COLUMN ""Email"" VARCHAR(100) NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Employees' AND column_name='Phone') THEN
                    ALTER TABLE ""Employees"" ADD COLUMN ""Phone"" VARCHAR(20) NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Shifts' AND column_name='IsDayOff') THEN
                    ALTER TABLE ""Shifts"" ADD COLUMN ""IsDayOff"" BOOLEAN DEFAULT FALSE NOT NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Shifts' AND column_name='OvernightHoursMode') THEN
                    ALTER TABLE ""Shifts"" ADD COLUMN ""OvernightHoursMode"" INTEGER DEFAULT 0 NOT NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Organizations' AND column_name='DefaultTemplatesInitialized') THEN
                    ALTER TABLE ""Organizations"" ADD COLUMN ""DefaultTemplatesInitialized"" BOOLEAN DEFAULT FALSE NOT NULL;
                END IF;
                IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Holidays' AND column_name='HalfDayStartTime') THEN
                    ALTER TABLE ""Holidays"" DROP COLUMN ""HalfDayStartTime"";
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Holidays' AND column_name='HalfDayWorkHours') THEN
                    ALTER TABLE ""Holidays"" ADD COLUMN ""HalfDayWorkHours"" DECIMAL NULL;
                END IF;
            END $$;
        ", "AddEmployeeColumns");
        
        // Create SystemSettings table FIRST (most critical)
        await SafeExecuteSql(@"
            CREATE TABLE IF NOT EXISTS ""SystemSettings"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""Key"" VARCHAR(100) NOT NULL UNIQUE,
                ""Value"" TEXT NOT NULL,
                ""Description"" VARCHAR(500) NULL,
                ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
            );
            CREATE INDEX IF NOT EXISTS ""IX_SystemSettings_Key"" ON ""SystemSettings"" (""Key"");
        ", "SystemSettings");
        
        // Create AdminUsers table SECOND (critical)
        await SafeExecuteSql(@"
            CREATE TABLE IF NOT EXISTS ""AdminUsers"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""Username"" VARCHAR(50) NOT NULL UNIQUE,
                ""PasswordHash"" VARCHAR(200) NOT NULL,
                ""FullName"" VARCHAR(100) NULL,
                ""Email"" VARCHAR(100) NULL,
                ""Role"" VARCHAR(20) NOT NULL DEFAULT 'Admin',
                ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
                ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                ""LastLoginAt"" TIMESTAMP WITH TIME ZONE NULL
            );
            CREATE INDEX IF NOT EXISTS ""IX_AdminUsers_Username"" ON ""AdminUsers"" (""Username"");
        ", "AdminUsers");
        
        // Create VisitorLogs table
        await SafeExecuteSql(@"
            CREATE TABLE IF NOT EXISTS ""VisitorLogs"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""IpAddress"" VARCHAR(45) NULL,
                ""UserAgent"" VARCHAR(500) NULL,
                ""Path"" VARCHAR(500) NULL,
                ""Referer"" VARCHAR(500) NULL,
                ""Country"" VARCHAR(100) NULL,
                ""City"" VARCHAR(100) NULL,
                ""SessionId"" VARCHAR(100) NULL,
                ""UserId"" VARCHAR(450) NULL,
                ""VisitedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                ""Duration"" INTEGER NULL,
                ""IsBot"" BOOLEAN NOT NULL DEFAULT FALSE
            );
            CREATE INDEX IF NOT EXISTS ""IX_VisitorLogs_VisitedAt"" ON ""VisitorLogs"" (""VisitedAt"");
            CREATE INDEX IF NOT EXISTS ""IX_VisitorLogs_SessionId"" ON ""VisitorLogs"" (""SessionId"");
        ", "VisitorLogs");
        
        // Create TimeAttendances table
        await SafeExecuteSql(@"
            CREATE TABLE IF NOT EXISTS ""TimeAttendances"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""EmployeeId"" INTEGER NOT NULL REFERENCES ""Employees""(""Id"") ON DELETE CASCADE,
                ""Date"" DATE NOT NULL,
                ""CheckInTime"" TIME NULL,
                ""CheckOutTime"" TIME NULL,
                ""CheckInFromPreviousDay"" BOOLEAN NOT NULL DEFAULT FALSE,
                ""CheckOutToNextDay"" BOOLEAN NOT NULL DEFAULT FALSE,
                ""Type"" INTEGER NOT NULL DEFAULT 0,
                ""Source"" INTEGER NOT NULL DEFAULT 0,
                ""SourceIdentifier"" VARCHAR(100) NULL,
                ""Notes"" VARCHAR(500) NULL,
                ""CheckInLocation"" VARCHAR(50) NULL,
                ""CheckOutLocation"" VARCHAR(50) NULL,
                ""WorkedHours"" DECIMAL NULL,
                ""IsApproved"" BOOLEAN NOT NULL DEFAULT TRUE,
                ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
            );
            CREATE INDEX IF NOT EXISTS ""IX_TimeAttendances_EmployeeId_Date"" ON ""TimeAttendances"" (""EmployeeId"", ""Date"");
            CREATE INDEX IF NOT EXISTS ""IX_TimeAttendances_Date"" ON ""TimeAttendances"" (""Date"");
        ", "TimeAttendances");
        
        // Create ApiKeys table
        await SafeExecuteSql(@"
            CREATE TABLE IF NOT EXISTS ""ApiKeys"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""OrganizationId"" INTEGER NOT NULL REFERENCES ""Organizations""(""Id"") ON DELETE CASCADE,
                ""KeyHash"" VARCHAR(64) NOT NULL,
                ""KeyPrefix"" VARCHAR(12) NOT NULL,
                ""Name"" VARCHAR(100) NOT NULL,
                ""Description"" VARCHAR(500) NULL,
                ""Permissions"" VARCHAR(500) NOT NULL,
                ""IpWhitelist"" VARCHAR(500) NULL,
                ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
                ""ExpiresAt"" TIMESTAMP WITH TIME ZONE NULL,
                ""LastUsedAt"" TIMESTAMP WITH TIME ZONE NULL,
                ""UsageCount"" INTEGER NOT NULL DEFAULT 0,
                ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ApiKeys_KeyHash"" ON ""ApiKeys"" (""KeyHash"");
            CREATE INDEX IF NOT EXISTS ""IX_ApiKeys_OrganizationId"" ON ""ApiKeys"" (""OrganizationId"");
        ", "ApiKeys");
        
        // Create SavedPayrolls table
        await SafeExecuteSql(@"
            CREATE TABLE IF NOT EXISTS ""SavedPayrolls"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""OrganizationId"" INTEGER NOT NULL REFERENCES ""Organizations""(""Id"") ON DELETE CASCADE,
                ""Name"" VARCHAR(100) NOT NULL,
                ""Year"" INTEGER NOT NULL,
                ""Month"" INTEGER NOT NULL,
                ""DataSource"" VARCHAR(20) NOT NULL DEFAULT 'shift',
                ""NightStartHour"" INTEGER NOT NULL DEFAULT 22,
                ""NightEndHour"" INTEGER NOT NULL DEFAULT 6,
                ""PayrollDataJson"" TEXT NOT NULL DEFAULT '[]',
                ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
            );
            CREATE INDEX IF NOT EXISTS ""IX_SavedPayrolls_OrganizationId"" ON ""SavedPayrolls"" (""OrganizationId"");
            CREATE INDEX IF NOT EXISTS ""IX_SavedPayrolls_OrgYearMonth"" ON ""SavedPayrolls"" (""OrganizationId"", ""Year"", ""Month"");
        ", "SavedPayrolls");
        
        // Create LeaveTypes table
        await SafeExecuteSql(@"
            CREATE TABLE IF NOT EXISTS ""LeaveTypes"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""OrganizationId"" INTEGER NULL REFERENCES ""Organizations""(""Id"") ON DELETE CASCADE,
                ""Code"" VARCHAR(10) NOT NULL,
                ""CodeEn"" VARCHAR(10) NOT NULL DEFAULT '',
                ""NameTr"" VARCHAR(100) NOT NULL,
                ""NameEn"" VARCHAR(100) NOT NULL,
                ""Category"" VARCHAR(30) NOT NULL DEFAULT 'other',
                ""IsPaid"" BOOLEAN NOT NULL DEFAULT TRUE,
                ""DefaultDays"" INTEGER NOT NULL DEFAULT 0,
                ""Color"" VARCHAR(10) NOT NULL DEFAULT '#9333ea',
                ""SortOrder"" INTEGER NOT NULL DEFAULT 0,
                ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
                ""IsSystem"" BOOLEAN NOT NULL DEFAULT FALSE
            );
            CREATE INDEX IF NOT EXISTS ""IX_LeaveTypes_OrganizationId"" ON ""LeaveTypes"" (""OrganizationId"");
            CREATE INDEX IF NOT EXISTS ""IX_LeaveTypes_IsSystem"" ON ""LeaveTypes"" (""IsSystem"");
        ", "LeaveTypes");
        
        // Add CodeEn column if not exists
        await SafeExecuteSql(@"
            DO $$ 
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LeaveTypes' AND column_name = 'CodeEn') THEN
                    ALTER TABLE ""LeaveTypes"" ADD COLUMN ""CodeEn"" VARCHAR(10) NOT NULL DEFAULT '';
                END IF;
            END $$;
        ", "LeaveTypesCodeEn");
        
        // Create Leaves table
        await SafeExecuteSql(@"
            CREATE TABLE IF NOT EXISTS ""Leaves"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""EmployeeId"" INTEGER NOT NULL REFERENCES ""Employees""(""Id"") ON DELETE CASCADE,
                ""LeaveTypeId"" INTEGER NOT NULL REFERENCES ""LeaveTypes""(""Id"") ON DELETE RESTRICT,
                ""Date"" DATE NOT NULL,
                ""Notes"" VARCHAR(500) NULL,
                ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
            );
            CREATE INDEX IF NOT EXISTS ""IX_Leaves_EmployeeId_Date"" ON ""Leaves"" (""EmployeeId"", ""Date"");
            CREATE INDEX IF NOT EXISTS ""IX_Leaves_Date"" ON ""Leaves"" (""Date"");
        ", "Leaves");
        
        // Migrate Leaves table from old structure (Type column) to new structure (LeaveTypeId)
        await SafeExecuteSql(@"
            DO $$ 
            BEGIN
                -- Check if old Type column exists and LeaveTypeId doesn't
                IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Leaves' AND column_name = 'Type')
                   AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Leaves' AND column_name = 'LeaveTypeId') THEN
                    -- Add LeaveTypeId column
                    ALTER TABLE ""Leaves"" ADD COLUMN ""LeaveTypeId"" INTEGER NULL;
                    
                    -- Migrate data: Type -> LeaveTypeId (assuming Type was an integer that maps to LeaveTypeId)
                    -- If Type was 0 or null, set to a default LeaveTypeId (first system leave type)
                    UPDATE ""Leaves"" SET ""LeaveTypeId"" = (
                        SELECT ""Id"" FROM ""LeaveTypes"" WHERE ""IsSystem"" = true ORDER BY ""SortOrder"" LIMIT 1
                    ) WHERE ""LeaveTypeId"" IS NULL;
                    
                    -- Make LeaveTypeId NOT NULL after migration
                    ALTER TABLE ""Leaves"" ALTER COLUMN ""LeaveTypeId"" SET NOT NULL;
                    
                    -- Add foreign key constraint
                    ALTER TABLE ""Leaves"" ADD CONSTRAINT ""FK_Leaves_LeaveTypes_LeaveTypeId"" 
                        FOREIGN KEY (""LeaveTypeId"") REFERENCES ""LeaveTypes""(""Id"") ON DELETE RESTRICT;
                    
                    -- Drop old Type column
                    ALTER TABLE ""Leaves"" DROP COLUMN ""Type"";
                END IF;
                
                -- Ensure LeaveTypeId column exists (for new installations)
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Leaves' AND column_name = 'LeaveTypeId') THEN
                    ALTER TABLE ""Leaves"" ADD COLUMN ""LeaveTypeId"" INTEGER NOT NULL REFERENCES ""LeaveTypes""(""Id"") ON DELETE RESTRICT;
                END IF;
                
                -- Migrate from StartDate/EndDate to Date if needed
                IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Leaves' AND column_name = 'StartDate')
                   AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Leaves' AND column_name = 'Date') THEN
                    ALTER TABLE ""Leaves"" ADD COLUMN ""Date"" DATE NULL;
                    UPDATE ""Leaves"" SET ""Date"" = ""StartDate"" WHERE ""Date"" IS NULL;
                    ALTER TABLE ""Leaves"" ALTER COLUMN ""Date"" SET NOT NULL;
                    ALTER TABLE ""Leaves"" DROP COLUMN ""StartDate"";
                    ALTER TABLE ""Leaves"" DROP COLUMN IF EXISTS ""EndDate"";
                END IF;
            END $$;
        ", "LeavesMigration");
        
        // Add new columns to AspNetUsers
        await SafeExecuteSql(@"
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'AspNetUsers' AND column_name = 'CustomEmployeeLimit') THEN
                    ALTER TABLE ""AspNetUsers"" ADD COLUMN ""CustomEmployeeLimit"" INTEGER NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'AspNetUsers' AND column_name = 'CanAccessAttendance') THEN
                    ALTER TABLE ""AspNetUsers"" ADD COLUMN ""CanAccessAttendance"" BOOLEAN DEFAULT TRUE NOT NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'AspNetUsers' AND column_name = 'CanAccessPayroll') THEN
                    ALTER TABLE ""AspNetUsers"" ADD COLUMN ""CanAccessPayroll"" BOOLEAN DEFAULT TRUE NOT NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'AspNetUsers' AND column_name = 'CanManageUnits') THEN
                    ALTER TABLE ""AspNetUsers"" ADD COLUMN ""CanManageUnits"" BOOLEAN DEFAULT FALSE NOT NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'AspNetUsers' AND column_name = 'AdminNotes') THEN
                    ALTER TABLE ""AspNetUsers"" ADD COLUMN ""AdminNotes"" VARCHAR(1000) NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'AspNetUsers' AND column_name = 'UnitLimit') THEN
                    ALTER TABLE ""AspNetUsers"" ADD COLUMN ""UnitLimit"" INTEGER DEFAULT 5 NOT NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'AspNetUsers' AND column_name = 'UnitEmployeeLimit') THEN
                    ALTER TABLE ""AspNetUsers"" ADD COLUMN ""UnitEmployeeLimit"" INTEGER DEFAULT 0 NOT NULL;
                END IF;
            END $$;
        ", "AspNetUsersColumns");
        
        // Create UnitTypes table (Premium feature)
        await SafeExecuteSql(@"
            CREATE TABLE IF NOT EXISTS ""UnitTypes"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""OrganizationId"" INTEGER NOT NULL REFERENCES ""Organizations""(""Id"") ON DELETE CASCADE,
                ""Name"" VARCHAR(100) NOT NULL,
                ""Description"" VARCHAR(500) NULL,
                ""DefaultCoefficient"" DECIMAL(5,2) NOT NULL DEFAULT 1.0,
                ""Color"" VARCHAR(20) NULL DEFAULT '#3B82F6',
                ""Icon"" VARCHAR(50) NULL,
                ""SortOrder"" INTEGER NOT NULL DEFAULT 0,
                ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
                ""IsSystem"" BOOLEAN NOT NULL DEFAULT FALSE,
                ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
            );
            CREATE INDEX IF NOT EXISTS ""IX_UnitTypes_OrganizationId"" ON ""UnitTypes"" (""OrganizationId"");
        ", "UnitTypes");
        
        // Add IsActive column to UnitTypes if not exists
        await SafeExecuteSql(@"
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'UnitTypes' AND column_name = 'IsActive') THEN
                    ALTER TABLE ""UnitTypes"" ADD COLUMN ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE;
                END IF;
            END $$;
        ", "UnitTypesIsActive");
        
        // Add NameEn column to UnitTypes if not exists (for English localization)
        await SafeExecuteSql(@"
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'UnitTypes' AND column_name = 'NameEn') THEN
                    ALTER TABLE ""UnitTypes"" ADD COLUMN ""NameEn"" VARCHAR(100) NULL;
                END IF;
            END $$;
        ", "UnitTypesNameEn");
        
        // Update existing default unit types with English names
        await SafeExecuteSql(@"
            UPDATE ""UnitTypes"" SET ""NameEn"" = 'Polyclinic/Service' WHERE ""Name"" = 'Poliklinik/Servis' AND ""NameEn"" IS NULL;
            UPDATE ""UnitTypes"" SET ""NameEn"" = 'Intensive Care Unit' WHERE ""Name"" = 'Yoğun Bakım' AND ""NameEn"" IS NULL;
            UPDATE ""UnitTypes"" SET ""NameEn"" = 'Radiation Unit' WHERE ""Name"" = 'Radyasyon Birimi' AND ""NameEn"" IS NULL;
            UPDATE ""UnitTypes"" SET ""NameEn"" = 'General Unit' WHERE ""Name"" = 'Genel Birim' AND ""NameEn"" IS NULL;
        ", "UnitTypesDefaultNameEn");
        
        // Create Units table (Premium feature)
        await SafeExecuteSql(@"
            CREATE TABLE IF NOT EXISTS ""Units"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""OrganizationId"" INTEGER NOT NULL REFERENCES ""Organizations""(""Id"") ON DELETE CASCADE,
                ""UnitTypeId"" INTEGER NULL REFERENCES ""UnitTypes""(""Id"") ON DELETE SET NULL,
                ""Name"" VARCHAR(100) NOT NULL,
                ""Description"" VARCHAR(500) NULL,
                ""Coefficient"" DECIMAL(5,2) NOT NULL DEFAULT 1.0,
                ""Color"" VARCHAR(20) NULL,
                ""IsDefault"" BOOLEAN NOT NULL DEFAULT FALSE,
                ""EmployeeLimit"" INTEGER NOT NULL DEFAULT 0,
                ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
                ""SortOrder"" INTEGER NOT NULL DEFAULT 0,
                ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
            );
            CREATE INDEX IF NOT EXISTS ""IX_Units_OrganizationId"" ON ""Units"" (""OrganizationId"");
            CREATE INDEX IF NOT EXISTS ""IX_Units_UnitTypeId"" ON ""Units"" (""UnitTypeId"");
        ", "Units");
        
        // Add missing columns to Units if table exists
        await SafeExecuteSql(@"
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Units' AND column_name = 'SortOrder') THEN
                    ALTER TABLE ""Units"" ADD COLUMN ""SortOrder"" INTEGER NOT NULL DEFAULT 0;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Units' AND column_name = 'UpdatedAt') THEN
                    ALTER TABLE ""Units"" ADD COLUMN ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW();
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Units' AND column_name = 'EmployeeLimit') THEN
                    ALTER TABLE ""Units"" ADD COLUMN ""EmployeeLimit"" INTEGER NOT NULL DEFAULT 0;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Units' AND column_name = 'IsActive') THEN
                    ALTER TABLE ""Units"" ADD COLUMN ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Units' AND column_name = 'UnitTypeId') THEN
                    ALTER TABLE ""Units"" ADD COLUMN ""UnitTypeId"" INTEGER NULL REFERENCES ""UnitTypes""(""Id"") ON DELETE SET NULL;
                    CREATE INDEX IF NOT EXISTS ""IX_Units_UnitTypeId"" ON ""Units"" (""UnitTypeId"");
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Units' AND column_name = 'IsDefault') THEN
                    ALTER TABLE ""Units"" ADD COLUMN ""IsDefault"" BOOLEAN NOT NULL DEFAULT FALSE;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Units' AND column_name = 'Coefficient') THEN
                    ALTER TABLE ""Units"" ADD COLUMN ""Coefficient"" DECIMAL(5,2) NOT NULL DEFAULT 1.0;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Units' AND column_name = 'Color') THEN
                    ALTER TABLE ""Units"" ADD COLUMN ""Color"" VARCHAR(20) NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Units' AND column_name = 'Description') THEN
                    ALTER TABLE ""Units"" ADD COLUMN ""Description"" VARCHAR(500) NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Units' AND column_name = 'CreatedAt') THEN
                    ALTER TABLE ""Units"" ADD COLUMN ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW();
                END IF;
            END $$;
        ", "UnitsColumns");
        
        // Add UnitId column to Employees table for unit assignment
        await SafeExecuteSql(@"
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Employees' AND column_name = 'UnitId') THEN
                    ALTER TABLE ""Employees"" ADD COLUMN ""UnitId"" INTEGER NULL REFERENCES ""Units""(""Id"") ON DELETE SET NULL;
                    CREATE INDEX IF NOT EXISTS ""IX_Employees_UnitId"" ON ""Employees"" (""UnitId"");
                END IF;
            END $$;
        ", "EmployeesUnitId");
        
        // Create Modules table (modular system)
        await SafeExecuteSql(@"
            CREATE TABLE IF NOT EXISTS ""Modules"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""Name"" VARCHAR(100) NOT NULL,
                ""Description"" VARCHAR(200) NULL,
                ""Code"" VARCHAR(50) NOT NULL UNIQUE,
                ""Icon"" VARCHAR(50) NULL,
                ""Color"" VARCHAR(7) NULL,
                ""SortOrder"" INTEGER NOT NULL DEFAULT 0,
                ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
                ""IsSystem"" BOOLEAN NOT NULL DEFAULT FALSE,
                ""IsPremium"" BOOLEAN NOT NULL DEFAULT FALSE,
                ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
            );
        ", "Modules");
        
        // Create SubModules table
        await SafeExecuteSql(@"
            CREATE TABLE IF NOT EXISTS ""SubModules"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""ModuleId"" INTEGER NOT NULL REFERENCES ""Modules""(""Id"") ON DELETE CASCADE,
                ""Name"" VARCHAR(100) NOT NULL,
                ""Description"" VARCHAR(200) NULL,
                ""Code"" VARCHAR(50) NOT NULL,
                ""Icon"" VARCHAR(50) NULL,
                ""RouteUrl"" VARCHAR(200) NULL,
                ""SortOrder"" INTEGER NOT NULL DEFAULT 0,
                ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
                ""IsSystem"" BOOLEAN NOT NULL DEFAULT FALSE,
                ""IsPremium"" BOOLEAN NOT NULL DEFAULT FALSE,
                ""RequiredPermission"" VARCHAR(100) NULL,
                ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                UNIQUE(""ModuleId"", ""Code"")
            );
            CREATE INDEX IF NOT EXISTS ""IX_SubModules_ModuleId"" ON ""SubModules"" (""ModuleId"");
        ", "SubModules");
        
        // Create UserModuleAccesses table
        await SafeExecuteSql(@"
            CREATE TABLE IF NOT EXISTS ""UserModuleAccesses"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""UserId"" VARCHAR(450) NOT NULL REFERENCES ""AspNetUsers""(""Id"") ON DELETE CASCADE,
                ""ModuleId"" INTEGER NOT NULL REFERENCES ""Modules""(""Id"") ON DELETE CASCADE,
                ""HasAccess"" BOOLEAN NOT NULL DEFAULT TRUE,
                ""AccessStartDate"" TIMESTAMP WITH TIME ZONE NULL,
                ""AccessEndDate"" TIMESTAMP WITH TIME ZONE NULL,
                ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                UNIQUE(""UserId"", ""ModuleId"")
            );
            CREATE INDEX IF NOT EXISTS ""IX_UserModuleAccesses_UserId"" ON ""UserModuleAccesses"" (""UserId"");
            CREATE INDEX IF NOT EXISTS ""IX_UserModuleAccesses_ModuleId"" ON ""UserModuleAccesses"" (""ModuleId"");
        ", "UserModuleAccesses");
        
        // Create UserApiCredentials table for API access
        await SafeExecuteSql(@"
            CREATE TABLE IF NOT EXISTS ""UserApiCredentials"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""UserId"" VARCHAR(450) NOT NULL REFERENCES ""AspNetUsers""(""Id"") ON DELETE CASCADE,
                ""OrganizationId"" INTEGER NOT NULL REFERENCES ""Organizations""(""Id"") ON DELETE CASCADE,
                ""ApiUsername"" VARCHAR(50) NOT NULL,
                ""ApiPasswordHash"" VARCHAR(100) NOT NULL,
                ""MonthlyRequestLimit"" INTEGER NOT NULL DEFAULT 0,
                ""CurrentMonthRequests"" INTEGER NOT NULL DEFAULT 0,
                ""MonthlyResetDate"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
                ""LastUsedAt"" TIMESTAMP WITH TIME ZONE NULL,
                ""TotalRequests"" INTEGER NOT NULL DEFAULT 0,
                ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                UNIQUE(""UserId"", ""OrganizationId""),
                UNIQUE(""ApiUsername"")
            );
            CREATE INDEX IF NOT EXISTS ""IX_UserApiCredentials_UserId"" ON ""UserApiCredentials"" (""UserId"");
            CREATE INDEX IF NOT EXISTS ""IX_UserApiCredentials_ApiUsername"" ON ""UserApiCredentials"" (""ApiUsername"");
        ", "UserApiCredentials");
        
        // Run migrations - but don't let failures prevent seeding
        try
        {
            await context.Database.MigrateAsync();
        }
        catch (Exception migrationEx)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(migrationEx, "Migration warning (tables may already exist).");
        }
        
        // Seed system settings (run regardless of migration status)
        try { await SeedSystemSettings(context); }
        catch (Exception ex) { Console.WriteLine($"SeedSystemSettings warning: {ex.Message}"); }
        
        // Seed admin users (run regardless of migration status)  
        try { await SeedAdminUsers(context); }
        catch (Exception ex) { Console.WriteLine($"SeedAdminUsers warning: {ex.Message}"); }
        
        // Seed leave types
        try { await SeedLeaveTypes(context); }
        catch (Exception ex) { Console.WriteLine($"SeedLeaveTypes warning: {ex.Message}"); }
        
        // Seed modules
        try { await SeedModules(context); }
        catch (Exception ex) { Console.WriteLine($"SeedModules warning: {ex.Message}"); }
        
        // Seed initial content pages
        try { await SeedContentPages(context); }
        catch (Exception ex) { Console.WriteLine($"SeedContentPages warning: {ex.Message}"); }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating or seeding the database.");
    }
}

app.Run();

// Seed method for system settings (limits, etc.)
static async Task SeedSystemSettings(ApplicationDbContext context)
{
    var existingSettings = await context.SystemSettings.ToListAsync();
    
    void AddSettingIfNotExists(string key, string value, string? description)
    {
        if (!existingSettings.Any(s => s.Key == key))
        {
            context.SystemSettings.Add(new SystemSettings
            {
                Key = key,
                Value = value,
                Description = description,
                UpdatedAt = DateTime.UtcNow
            });
        }
    }
    
    // Employee limits
    AddSettingIfNotExists(SystemSettings.Keys.GuestEmployeeLimit, "5", "Kayıtsız kullanıcılar için personel limiti");
    AddSettingIfNotExists(SystemSettings.Keys.RegisteredEmployeeLimit, "10", "Kayıtlı kullanıcılar için personel limiti");
    AddSettingIfNotExists(SystemSettings.Keys.PremiumEmployeeLimit, "100", "Premium kullanıcılar için personel limiti");
    AddSettingIfNotExists(SystemSettings.Keys.SiteName, "Geldimmi", "Site adı");
    AddSettingIfNotExists(SystemSettings.Keys.MaintenanceMode, "false", "Bakım modu aktif mi?");
    
    await context.SaveChangesAsync();
}

// Seed method for admin users
static async Task SeedAdminUsers(ApplicationDbContext context)
{
    var existingAdmins = await context.AdminUsers.ToListAsync();
    
    // Create default SuperAdmin if no admin exists
    if (!existingAdmins.Any())
    {
        context.AdminUsers.Add(new AdminUser
        {
            Username = "GeldimmiX",
            PasswordHash = AdminUser.HashPassword("Liberemall423445"),
            FullName = "Super Admin",
            Role = AdminRoles.SuperAdmin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        
        await context.SaveChangesAsync();
    }
}

// Seed method for leave types
static async Task SeedLeaveTypes(ApplicationDbContext context)
{
    // Check if system leave types already exist
    var existingSystemTypes = await context.LeaveTypes.Where(lt => lt.IsSystem).ToListAsync();
    
    // If leave types exist but some are missing CodeEn, update them
    if (existingSystemTypes.Any())
    {
        var needsUpdate = existingSystemTypes.Where(lt => string.IsNullOrEmpty(lt.CodeEn)).ToList();
        if (needsUpdate.Any())
        {
            var codeMapping = new Dictionary<string, string>
            {
                { "Yİ", "AL" }, { "Yol İ", "TL" }, { "DÖİ", "PRE" }, { "DSİ", "POST" },
                { "Çİ", "MPL" }, { "Bİ", "PL" }, { "Sİ", "NL" }, { "ÜAİ", "UML" },
                { "Eİ", "ADL" }, { "PKİ", "PCL" }, { "Rp", "SL" }, { "Rİ", "CL" },
                { "İKİ", "WAL" }, { "MHİ", "ODL" }, { "PMİ", "MED" }, { "Evİ", "ML" },
                { "Vİ", "BR" }, { "Mİ", "EL" }, { "Snİ", "EXM" }, { "Eğİ", "EDU" },
                { "Aİ", "MIL" }, { "BAİ", "PMS" }, { "İİ", "ADM" }, { "RTİ", "PH" },
                { "OHİ", "EMG" }, { "İAİ", "JSL" }, { "Üİ", "UL" }, { "Kİ", "DC" },
                { "Sdİ", "UNL" }, { "Şİ", "WIT" }, { "Oİ", "VOT" }, { "HTİ", "MTL" },
                { "Afİ", "DIS" }
            };
            
            foreach (var lt in needsUpdate)
            {
                if (codeMapping.TryGetValue(lt.Code, out var codeEn))
                {
                    lt.CodeEn = codeEn;
                }
            }
            await context.SaveChangesAsync();
        }
        return;
    }

    var leaveTypes = new List<LeaveType>
    {
        // 1. Yıllık İzinler (Annual Leaves)
        new() { Code = "Yİ", CodeEn = "AL", NameTr = "Yıllık Ücretli İzin", NameEn = "Annual Paid Leave", Category = "annual", IsPaid = true, DefaultDays = 14, Color = "#22c55e", SortOrder = 1, IsSystem = true },
        new() { Code = "Yol İ", CodeEn = "TL", NameTr = "Yol İzni", NameEn = "Travel Leave", Category = "annual", IsPaid = true, DefaultDays = 4, Color = "#16a34a", SortOrder = 2, IsSystem = true },
        
        // 2. Analık/Babalık İzinleri (Maternity/Paternity)
        new() { Code = "DÖİ", CodeEn = "PRE", NameTr = "Doğum Öncesi İzin", NameEn = "Prenatal Leave", Category = "maternity", IsPaid = true, DefaultDays = 56, Color = "#ec4899", SortOrder = 10, IsSystem = true },
        new() { Code = "DSİ", CodeEn = "POST", NameTr = "Doğum Sonrası İzin", NameEn = "Postnatal Leave", Category = "maternity", IsPaid = true, DefaultDays = 56, Color = "#db2777", SortOrder = 11, IsSystem = true },
        new() { Code = "Çİ", CodeEn = "MPL", NameTr = "Çoğul Gebelik Ek İzni", NameEn = "Multiple Pregnancy Extra Leave", Category = "maternity", IsPaid = true, DefaultDays = 14, Color = "#be185d", SortOrder = 12, IsSystem = true },
        new() { Code = "Bİ", CodeEn = "PL", NameTr = "Babalık İzni", NameEn = "Paternity Leave", Category = "maternity", IsPaid = true, DefaultDays = 5, Color = "#3b82f6", SortOrder = 13, IsSystem = true },
        new() { Code = "Sİ", CodeEn = "NL", NameTr = "Süt İzni", NameEn = "Nursing Leave", Category = "maternity", IsPaid = true, DefaultDays = 0, Color = "#f472b6", SortOrder = 14, IsSystem = true },
        new() { Code = "ÜAİ", CodeEn = "UML", NameTr = "Ücretsiz Analık İzni", NameEn = "Unpaid Maternity Leave", Category = "maternity", IsPaid = false, DefaultDays = 180, Color = "#9d174d", SortOrder = 15, IsSystem = true },
        new() { Code = "Eİ", CodeEn = "ADL", NameTr = "Evlat Edinme İzni", NameEn = "Adoption Leave", Category = "maternity", IsPaid = true, DefaultDays = 56, Color = "#a855f7", SortOrder = 16, IsSystem = true },
        new() { Code = "PKİ", CodeEn = "PCL", NameTr = "Periyodik Kontrol İzni", NameEn = "Periodic Checkup Leave", Category = "maternity", IsPaid = true, DefaultDays = 0, Color = "#c084fc", SortOrder = 17, IsSystem = true },
        
        // 3. Sağlık İzinleri (Health Leaves)
        new() { Code = "Rp", CodeEn = "SL", NameTr = "Rapor (Hastalık İzni)", NameEn = "Sick Leave (Medical Report)", Category = "health", IsPaid = true, DefaultDays = 0, Color = "#ef4444", SortOrder = 20, IsSystem = true },
        new() { Code = "Rİ", CodeEn = "CL", NameTr = "Refakat İzni", NameEn = "Compassionate Leave", Category = "health", IsPaid = true, DefaultDays = 90, Color = "#f97316", SortOrder = 21, IsSystem = true },
        new() { Code = "İKİ", CodeEn = "WAL", NameTr = "İş Kazası İzni", NameEn = "Work Accident Leave", Category = "health", IsPaid = true, DefaultDays = 0, Color = "#dc2626", SortOrder = 22, IsSystem = true },
        new() { Code = "MHİ", CodeEn = "ODL", NameTr = "Meslek Hastalığı İzni", NameEn = "Occupational Disease Leave", Category = "health", IsPaid = true, DefaultDays = 0, Color = "#b91c1c", SortOrder = 23, IsSystem = true },
        new() { Code = "PMİ", CodeEn = "MED", NameTr = "Periyodik Muayene İzni", NameEn = "Medical Examination Leave", Category = "health", IsPaid = true, DefaultDays = 0, Color = "#fb923c", SortOrder = 24, IsSystem = true },
        
        // 4. Mazeret İzinleri (Excuse Leaves)
        new() { Code = "Evİ", CodeEn = "ML", NameTr = "Evlilik İzni", NameEn = "Marriage Leave", Category = "excuse", IsPaid = true, DefaultDays = 3, Color = "#e11d48", SortOrder = 30, IsSystem = true },
        new() { Code = "Vİ", CodeEn = "BR", NameTr = "Vefat/Ölüm İzni", NameEn = "Bereavement Leave", Category = "excuse", IsPaid = true, DefaultDays = 3, Color = "#1f2937", SortOrder = 31, IsSystem = true },
        new() { Code = "Mİ", CodeEn = "EL", NameTr = "Mazeret İzni", NameEn = "Excuse Leave", Category = "excuse", IsPaid = true, DefaultDays = 0, Color = "#6b7280", SortOrder = 32, IsSystem = true },
        
        // 5. Eğitim ve Sınav İzinleri (Education)
        new() { Code = "Snİ", CodeEn = "EXM", NameTr = "Sınav İzni", NameEn = "Exam Leave", Category = "education", IsPaid = true, DefaultDays = 0, Color = "#8b5cf6", SortOrder = 40, IsSystem = true },
        new() { Code = "Eğİ", CodeEn = "EDU", NameTr = "Eğitim İzni", NameEn = "Education Leave", Category = "education", IsPaid = true, DefaultDays = 0, Color = "#7c3aed", SortOrder = 41, IsSystem = true },
        
        // 6. Askerlik İzinleri (Military)
        new() { Code = "Aİ", CodeEn = "MIL", NameTr = "Askerlik İzni", NameEn = "Military Service Leave", Category = "military", IsPaid = false, DefaultDays = 0, Color = "#047857", SortOrder = 50, IsSystem = true },
        new() { Code = "BAİ", CodeEn = "PMS", NameTr = "Bedelli Askerlik İzni", NameEn = "Paid Military Service Leave", Category = "military", IsPaid = false, DefaultDays = 30, Color = "#059669", SortOrder = 51, IsSystem = true },
        
        // 7. İdari İzinler (Administrative)
        new() { Code = "İİ", CodeEn = "ADM", NameTr = "İdari İzin", NameEn = "Administrative Leave", Category = "administrative", IsPaid = true, DefaultDays = 0, Color = "#0ea5e9", SortOrder = 60, IsSystem = true },
        new() { Code = "RTİ", CodeEn = "PH", NameTr = "Resmi Tatil İzni", NameEn = "Public Holiday Leave", Category = "administrative", IsPaid = true, DefaultDays = 0, Color = "#0284c7", SortOrder = 61, IsSystem = true },
        new() { Code = "OHİ", CodeEn = "EMG", NameTr = "Olağanüstü Hal İzni", NameEn = "Emergency Leave", Category = "administrative", IsPaid = true, DefaultDays = 0, Color = "#0369a1", SortOrder = 62, IsSystem = true },
        
        // 8. İş Arama İzni (Job Search)
        new() { Code = "İAİ", CodeEn = "JSL", NameTr = "İş Arama İzni", NameEn = "Job Search Leave", Category = "other", IsPaid = true, DefaultDays = 0, Color = "#64748b", SortOrder = 70, IsSystem = true },
        
        // 9. Ücretsiz İzinler (Unpaid)
        new() { Code = "Üİ", CodeEn = "UL", NameTr = "Ücretsiz İzin", NameEn = "Unpaid Leave", Category = "unpaid", IsPaid = false, DefaultDays = 0, Color = "#78716c", SortOrder = 80, IsSystem = true },
        
        // 10. Diğer İzinler (Other)
        new() { Code = "Kİ", CodeEn = "DC", NameTr = "Kreş İzni", NameEn = "Daycare Leave", Category = "other", IsPaid = true, DefaultDays = 0, Color = "#f59e0b", SortOrder = 90, IsSystem = true },
        new() { Code = "Sdİ", CodeEn = "UNL", NameTr = "Sendika İzni", NameEn = "Union Leave", Category = "other", IsPaid = true, DefaultDays = 0, Color = "#d97706", SortOrder = 91, IsSystem = true },
        new() { Code = "Şİ", CodeEn = "WIT", NameTr = "Şahit/Tanıklık İzni", NameEn = "Witness Leave", Category = "other", IsPaid = true, DefaultDays = 0, Color = "#92400e", SortOrder = 92, IsSystem = true },
        new() { Code = "Oİ", CodeEn = "VOT", NameTr = "Oy Kullanma İzni", NameEn = "Voting Leave", Category = "other", IsPaid = true, DefaultDays = 1, Color = "#1e40af", SortOrder = 93, IsSystem = true },
        new() { Code = "HTİ", CodeEn = "MTL", NameTr = "Hekim/Tedavi İzni", NameEn = "Medical Treatment Leave", Category = "other", IsPaid = true, DefaultDays = 0, Color = "#dc2626", SortOrder = 94, IsSystem = true },
        new() { Code = "Afİ", CodeEn = "DIS", NameTr = "Afet İzni", NameEn = "Disaster Leave", Category = "other", IsPaid = true, DefaultDays = 0, Color = "#b45309", SortOrder = 95, IsSystem = true },
    };

    context.LeaveTypes.AddRange(leaveTypes);
    await context.SaveChangesAsync();
}

// Seed method for modules
static async Task SeedModules(ApplicationDbContext context)
{
    // Check if we already have the main module
    if (await context.Modules.AnyAsync(m => m.Code == "nurse-shift"))
        return;
    
    // Create "Hemşire Nöbet Sistemi" main module
    var nurseShiftModule = new Module
    {
        Name = "Hemşire Nöbet Sistemi",
        Description = "Sağlık personeli için kapsamlı nöbet ve mesai yönetim sistemi",
        Code = "nurse-shift",
        Icon = "🏥",
        Color = "#3B82F6",
        SortOrder = 1,
        IsActive = true,
        IsSystem = true,
        IsPremium = false
    };
    
    context.Modules.Add(nurseShiftModule);
    await context.SaveChangesAsync();
    
    // Create sub-modules
    var subModules = new List<SubModule>
    {
        new SubModule
        {
            ModuleId = nurseShiftModule.Id,
            Name = "Nöbet Yönetimi",
            Description = "Nöbet listesi oluşturma ve personel atama",
            Code = "shifts",
            Icon = "📅",
            RouteUrl = "/app",
            SortOrder = 1,
            IsActive = true,
            IsSystem = true,
            IsPremium = false
        },
        new SubModule
        {
            ModuleId = nurseShiftModule.Id,
            Name = "Personel Yönetimi",
            Description = "Personel ekleme, düzenleme ve birim atama",
            Code = "employees",
            Icon = "👥",
            RouteUrl = "/app",
            SortOrder = 2,
            IsActive = true,
            IsSystem = true,
            IsPremium = false
        },
        new SubModule
        {
            ModuleId = nurseShiftModule.Id,
            Name = "Vardiya Şablonları",
            Description = "Özel vardiya şablonları tanımlama",
            Code = "templates",
            Icon = "⏰",
            RouteUrl = "/app",
            SortOrder = 3,
            IsActive = true,
            IsSystem = true,
            IsPremium = false
        },
        new SubModule
        {
            ModuleId = nurseShiftModule.Id,
            Name = "İzin Yönetimi",
            Description = "Personel izinlerini takip ve yönetim",
            Code = "leaves",
            Icon = "🏖️",
            RouteUrl = "/app",
            SortOrder = 4,
            IsActive = true,
            IsSystem = true,
            IsPremium = false
        },
        new SubModule
        {
            ModuleId = nurseShiftModule.Id,
            Name = "Resmi Tatiller",
            Description = "Resmi tatil ve özel gün tanımlama",
            Code = "holidays",
            Icon = "🎉",
            RouteUrl = "/app",
            SortOrder = 5,
            IsActive = true,
            IsSystem = true,
            IsPremium = false
        },
        new SubModule
        {
            ModuleId = nurseShiftModule.Id,
            Name = "Mesai Takip",
            Description = "Günlük ve aylık mesai saati takibi",
            Code = "attendance",
            Icon = "🕐",
            RouteUrl = "/app/attendance",
            SortOrder = 6,
            IsActive = true,
            IsSystem = true,
            IsPremium = false,
            RequiredPermission = "CanAccessAttendance"
        },
        new SubModule
        {
            ModuleId = nurseShiftModule.Id,
            Name = "Puantaj",
            Description = "Aylık puantaj hesaplama ve raporlama",
            Code = "timesheet",
            Icon = "📊",
            RouteUrl = "/app/timesheet",
            SortOrder = 7,
            IsActive = true,
            IsSystem = true,
            IsPremium = false,
            RequiredPermission = "CanAccessPayroll"
        },
        new SubModule
        {
            ModuleId = nurseShiftModule.Id,
            Name = "Birim Yönetimi",
            Description = "Birim oluşturma ve personel organizasyonu",
            Code = "units",
            Icon = "🏛️",
            RouteUrl = "/app",
            SortOrder = 8,
            IsActive = true,
            IsSystem = true,
            IsPremium = true,
            RequiredPermission = "CanManageUnits"
        },
        new SubModule
        {
            ModuleId = nurseShiftModule.Id,
            Name = "Raporlar",
            Description = "Detaylı nöbet ve mesai raporları",
            Code = "reports",
            Icon = "📈",
            RouteUrl = "/app/reports",
            SortOrder = 9,
            IsActive = true,
            IsSystem = true,
            IsPremium = false
        },
        new SubModule
        {
            ModuleId = nurseShiftModule.Id,
            Name = "Excel Export",
            Description = "Verileri Excel formatında dışa aktarma",
            Code = "export",
            Icon = "📥",
            RouteUrl = "/app",
            SortOrder = 10,
            IsActive = true,
            IsSystem = true,
            IsPremium = false
        }
    };
    
    context.SubModules.AddRange(subModules);
    await context.SaveChangesAsync();
}

// Seed method for content pages
static async Task SeedContentPages(ApplicationDbContext context)
{
    // Get all existing pages first to check what we have
    var existingPages = await context.ContentPages
        .Select(p => new { p.Slug, p.Language })
        .ToListAsync();
    
    var existingKeys = existingPages
        .Select(p => $"{p.Slug}:{p.Language}")
        .ToHashSet();

    var contentPages = new List<ContentPage>
    {
        // ==================== TURKISH PAGES ====================
        
        // 1. Nöbet Listesi Oluşturma
        new ContentPage
        {
            Slug = "nobet-listesi-olusturma",
            Language = "tr",
            Title = "Online Nöbet Listesi Oluşturma",
            MetaDescription = "Ücretsiz online nöbet listesi oluşturun. Hastane, fabrika, güvenlik ve tüm sektörler için akıllı nöbet planlama sistemi. Kayıt olmadan hemen başlayın.",
            MetaKeywords = "nöbet listesi, nöbet programı, vardiya planlama, nöbet çizelgesi, online nöbet, ücretsiz nöbet programı",
            Subtitle = "Saniyeler içinde profesyonel nöbet listeleri oluşturun",
            CtaText = "Hemen Ücretsiz Başla",
            CtaUrl = "/app",
            PageType = PageType.Feature,
            Content = @"<div class='feature-content'>
<h2>Nöbet Listesi Nedir?</h2>
<p>Nöbet listesi, bir kurumdaki personelin hangi gün ve saatlerde çalışacağını gösteren planlama aracıdır. Hastaneler, fabrikalar, güvenlik şirketleri ve 7/24 hizmet veren tüm işletmeler için vazgeçilmezdir.</p>

<h2>Geldimmi ile Nöbet Listesi Oluşturma</h2>
<p>Geldimmi, nöbet listesi oluşturmayı son derece kolay hale getirir:</p>
<ul>
    <li><strong>Hızlı Personel Ekleme:</strong> Excel'den kopyala-yapıştır ile anında personel ekleyin</li>
    <li><strong>Esnek Vardiya Şablonları:</strong> Sabah, akşam, gece veya özel vardiyalar tanımlayın</li>
    <li><strong>Sürükle-Bırak Atama:</strong> Takvim üzerinde kolayca nöbet atayın</li>
    <li><strong>Akıllı Dağıtım:</strong> Algoritmamız nöbetleri adil şekilde dağıtır</li>
</ul>

<h2>Özellikler</h2>
<h3>📅 Aylık Takvim Görünümü</h3>
<p>Tüm ayı tek bakışta görün. Kimin ne zaman çalıştığını anında takip edin.</p>

<h3>🎨 Renk Kodlama</h3>
<p>Farklı vardiya türlerini renklerle ayırt edin. Sabah mavisi, gece moru gibi.</p>

<h3>📱 Mobil Uyumlu</h3>
<p>Telefonunuzdan veya tabletinizden nöbet listesi oluşturun ve paylaşın.</p>

<h3>📥 Excel Export</h3>
<p>Oluşturduğunuz nöbet listesini tek tıkla Excel'e aktarın.</p>

<h2>Kimler İçin?</h2>
<ul>
    <li>Hastane ve sağlık kuruluşları</li>
    <li>Fabrika ve üretim tesisleri</li>
    <li>Güvenlik şirketleri</li>
    <li>Çağrı merkezleri</li>
    <li>Otel ve turizm işletmeleri</li>
    <li>Market ve perakende zincirleri</li>
</ul>
</div>",
            DisplayOrder = 1,
            IsPublished = true
        },

        // 2. Hemşire Nöbet Programı
        new ContentPage
        {
            Slug = "hemsire-nobet-programi",
            Language = "tr",
            Title = "Hemşire Nöbet Programı",
            MetaDescription = "Hastaneler için özel hemşire nöbet planlama sistemi. Adil dağıtım algoritması, gece nöbeti takibi ve otomatik puantaj. Ücretsiz deneyin.",
            MetaKeywords = "hemşire nöbet programı, hastane nöbet listesi, hemşire vardiya, sağlık personeli nöbet, hemşire çalışma saatleri",
            Subtitle = "Hastaneler için özel tasarlanmış akıllı nöbet sistemi",
            CtaText = "Ücretsiz Dene",
            CtaUrl = "/app",
            PageType = PageType.Feature,
            Content = @"<div class='feature-content'>
<h2>Hemşireler İçin Özel Çözüm</h2>
<p>Hemşire nöbet planlaması, sağlık sektörünün en zorlu konularından biridir. Geldimmi, hemşirelerin iş yükünü dengelemek ve adil bir çalışma ortamı sağlamak için özel olarak tasarlanmıştır.</p>

<h2>Hemşire Nöbet Planlamasının Zorlukları</h2>
<ul>
    <li>Gece nöbetlerinin adil dağıtılması</li>
    <li>Hafta sonu çalışmalarının dengelenmesi</li>
    <li>Ardışık nöbet kontrolü</li>
    <li>Yasal dinlenme sürelerine uyum</li>
    <li>Acil durum ve izin yönetimi</li>
</ul>

<h2>Geldimmi Nasıl Yardımcı Olur?</h2>
<h3>⚖️ Adil Dağıtım Algoritması</h3>
<p>Akıllı algoritmamız, gece nöbetlerini ve hafta sonu çalışmalarını tüm hemşireler arasında eşit dağıtır.</p>

<h3>🌙 Gece Nöbeti Takibi</h3>
<p>Her hemşirenin kaç gece nöbeti tuttuğunu otomatik hesaplar ve puantaja yansıtır.</p>

<h3>📊 Detaylı Puantaj</h3>
<p>Normal çalışma, gece çalışması, hafta sonu ve fazla mesai saatlerini ayrı ayrı hesaplar.</p>

<h3>🔄 16 Saatlik Nöbet Desteği</h3>
<p>Hemşire nöbetlerinde sık kullanılan 16:00-08:00 gibi ertesi güne sarkan vardiyaları destekler.</p>

<h2>Örnek Hemşire Nöbet Planı</h2>
<p>10 hemşireli bir serviste, sistemimiz otomatik olarak:</p>
<ul>
    <li>Her hemşireye ayda ortalama 4-5 gece nöbeti atar</li>
    <li>Hafta sonu çalışmalarını dengeler</li>
    <li>Ardışık gece nöbeti oluşmasını engeller</li>
    <li>İzin ve raporları dikkate alır</li>
</ul>
</div>",
            DisplayOrder = 2,
            IsPublished = true
        },

        // 3. Adil Nöbet Dağıtımı
        new ContentPage
        {
            Slug = "adil-nobet-dagitimi",
            Language = "tr",
            Title = "Adil Nöbet Dağıtım Sistemi",
            MetaDescription = "Akıllı algoritma ile adil nöbet dağıtımı. Gece, hafta sonu ve tatil nöbetlerini dengeli şekilde planlayın. Çalışan memnuniyetini artırın.",
            MetaKeywords = "adil nöbet dağıtımı, nöbet algoritması, dengeli vardiya, eşit nöbet, nöbet adaleti",
            Subtitle = "Akıllı algoritma ile dengeli ve adil nöbet planlaması",
            CtaText = "Şimdi Dene",
            CtaUrl = "/app",
            PageType = PageType.Feature,
            Content = @"<div class='feature-content'>
<h2>Nöbet Dağıtımında Adalet Neden Önemli?</h2>
<p>Adaletsiz nöbet dağıtımı, çalışan memnuniyetsizliği, motivasyon kaybı ve hatta işten ayrılmalara neden olabilir. Geldimmi'nin akıllı algoritması bu sorunu çözer.</p>

<h2>Adil Dağıtım Kriterleri</h2>
<ul>
    <li><strong>Gece Nöbetleri:</strong> Her çalışana eşit sayıda gece nöbeti</li>
    <li><strong>Hafta Sonu:</strong> Cumartesi ve Pazar çalışmalarının dengeli dağıtımı</li>
    <li><strong>Resmi Tatiller:</strong> Bayram ve tatil günlerinin adil paylaşımı</li>
    <li><strong>Toplam Çalışma Saati:</strong> Aylık çalışma sürelerinin dengelenmesi</li>
</ul>

<h2>Algoritmamız Nasıl Çalışır?</h2>
<h3>1. Veri Toplama</h3>
<p>Geçmiş nöbet verilerini ve çalışan tercihlerini analiz eder.</p>

<h3>2. Kısıtları Belirleme</h3>
<p>İzinler, raporlar ve yasal dinlenme süreleri hesaba katılır.</p>

<h3>3. Optimizasyon</h3>
<p>En adil dağıtımı bulmak için matematiksel optimizasyon uygulanır.</p>

<h3>4. Dengeleme</h3>
<p>Gece, hafta sonu ve tatil nöbetleri tüm personel arasında eşitlenir.</p>

<h2>Sonuçlar</h2>
<ul>
    <li>✅ %95 daha az nöbet şikayeti</li>
    <li>✅ Çalışan memnuniyetinde artış</li>
    <li>✅ Yönetici iş yükünde azalma</li>
    <li>✅ Şeffaf ve ölçülebilir dağıtım</li>
</ul>
</div>",
            DisplayOrder = 3,
            IsPublished = true
        },

        // 4. Puantaj Hesaplama
        new ContentPage
        {
            Slug = "puantaj-hesaplama",
            Language = "tr",
            Title = "Online Puantaj Hesaplama",
            MetaDescription = "Nöbet listesinden otomatik puantaj oluşturun. Fazla mesai, gece çalışması, hafta sonu ve tatil saatlerini ayrı ayrı hesaplayın. Excel export.",
            MetaKeywords = "puantaj hesaplama, puantaj oluşturma, mesai hesabı, çalışma saati hesaplama, otomatik puantaj",
            Subtitle = "Nöbet listesinden otomatik puantaj ve mesai hesabı",
            CtaText = "Puantaj Oluştur",
            CtaUrl = "/app",
            PageType = PageType.Feature,
            Content = @"<div class='feature-content'>
<h2>Puantaj Nedir?</h2>
<p>Puantaj, personelin aylık çalışma saatlerini ve türlerini gösteren dokümandır. Bordro hesaplamasının temelini oluşturur ve yasal bir zorunluluktur.</p>

<h2>Geldimmi ile Otomatik Puantaj</h2>
<p>Nöbet listesi oluşturduktan sonra, tek tıkla detaylı puantaj raporunuzu alın:</p>

<h3>📊 Hesaplanan Değerler</h3>
<ul>
    <li><strong>Normal Çalışma:</strong> Standart mesai saatleri içindeki çalışma</li>
    <li><strong>Gece Çalışması:</strong> 20:00-06:00 arası çalışma saatleri</li>
    <li><strong>Hafta Sonu:</strong> Cumartesi ve Pazar günleri çalışma</li>
    <li><strong>Resmi Tatil:</strong> Bayram ve resmi tatil günleri çalışma</li>
    <li><strong>Fazla Mesai:</strong> Günlük veya aylık limite göre hesaplanan ek çalışma</li>
</ul>

<h3>⚙️ Hesaplama Modları</h3>
<p><strong>Günlük Mod:</strong> Her gün için ayrı fazla mesai hesabı. Örneğin, günlük 8 saat çalışması gereken biri 10 saat çalıştıysa, o gün 2 saat fazla mesai yazılır.</p>
<p><strong>Aylık Mod:</strong> Ay sonunda toplam çalışma saatine bakılır. Aylık hedef 176 saat, toplam çalışma 180 saat ise, 4 saat fazla mesai hesaplanır.</p>

<h3>🔧 Ayarlanabilir Parametreler</h3>
<ul>
    <li>Gece başlangıç/bitiş saatleri (örn: 20:00-06:00)</li>
    <li>Günlük çalışma hedefi (örn: 8 saat)</li>
    <li>Aylık çalışma hedefi (örn: 176 saat)</li>
    <li>Mola süreleri</li>
</ul>

<h2>Excel Export</h2>
<p>Oluşturulan puantajı tek tıkla Excel'e aktarın. Bordro sistemlerinize kolayca entegre edin.</p>
</div>",
            DisplayOrder = 4,
            IsPublished = true
        },

        // 5. Fazla Mesai Hesaplama
        new ContentPage
        {
            Slug = "fazla-mesai-hesaplama",
            Language = "tr",
            Title = "Fazla Mesai Hesaplama Sistemi",
            MetaDescription = "Günlük ve aylık fazla mesai hesaplama. Otomatik overtime takibi, yasal sınırlar ve raporlama. İş Kanunu'na uygun hesaplama.",
            MetaKeywords = "fazla mesai hesaplama, overtime hesabı, ek mesai, mesai ücreti, fazla çalışma",
            Subtitle = "Günlük veya aylık modda otomatik fazla mesai hesabı",
            CtaText = "Hesaplamaya Başla",
            CtaUrl = "/app",
            PageType = PageType.Feature,
            Content = @"<div class='feature-content'>
<h2>Fazla Mesai Nedir?</h2>
<p>Fazla mesai (overtime), çalışanın yasal veya sözleşmesel çalışma süresini aşan çalışmasıdır. Türkiye'de İş Kanunu'na göre haftalık 45 saati aşan çalışmalar fazla mesai sayılır.</p>

<h2>İki Farklı Hesaplama Modu</h2>
<h3>📅 Günlük Hesaplama</h3>
<p>Her gün için ayrı ayrı fazla mesai hesaplanır:</p>
<ul>
    <li>Günlük çalışma hedefi: 8 saat</li>
    <li>Bugün çalışılan: 11 saat</li>
    <li>Fazla mesai: 3 saat</li>
</ul>
<p><em>Avantajı: Her günün fazla mesaisi net görülür</em></p>

<h3>📆 Aylık Hesaplama</h3>
<p>Ay sonunda toplam saat üzerinden hesaplanır:</p>
<ul>
    <li>Aylık çalışma hedefi: 176 saat</li>
    <li>Toplam çalışılan: 184 saat</li>
    <li>Fazla mesai: 8 saat</li>
</ul>
<p><em>Avantajı: Bazı günler fazla, bazı günler eksik çalışma dengelenir</em></p>

<h2>Gece Çalışması ve Fazla Mesai</h2>
<p>Örnek: Bir hemşire 16:00-08:00 (16 saat) nöbet tutmuş.</p>
<ul>
    <li>Normal çalışma hedefi: 8 saat/gün × 2 gün = 16 saat</li>
    <li>Çalışılan: 16 saat</li>
    <li>Fazla mesai: 0 saat</li>
    <li>Gece çalışması (20:00-06:00): 10 saat</li>
</ul>
<p>Bu durumda fazla mesai yoktur, ancak 10 saat gece çalışması tazminatı uygulanabilir.</p>

<h2>Yasal Sınırlar</h2>
<ul>
    <li>Günlük fazla mesai: Maksimum 3 saat</li>
    <li>Yıllık fazla mesai: Maksimum 270 saat</li>
    <li>Fazla mesai ücreti: Normal ücretin %50 fazlası</li>
</ul>
</div>",
            DisplayOrder = 5,
            IsPublished = true
        },

        // ==================== ENGLISH PAGES ====================

        // 1. Shift Scheduling
        new ContentPage
        {
            Slug = "shift-scheduling",
            Language = "en",
            Title = "Online Shift Scheduling Software",
            MetaDescription = "Free online shift scheduling tool. Create employee schedules for hospitals, factories, and businesses. Smart algorithm for fair distribution. Start without registration.",
            MetaKeywords = "shift scheduling, employee scheduling, work schedule maker, duty roster, shift planner, free scheduling software",
            Subtitle = "Create professional shift schedules in seconds",
            CtaText = "Start Free Now",
            CtaUrl = "/app",
            PageType = PageType.Feature,
            Content = @"<div class='feature-content'>
<h2>What is Shift Scheduling?</h2>
<p>Shift scheduling is the process of creating work schedules that assign employees to specific shifts. It's essential for hospitals, factories, security companies, and any business operating 24/7.</p>

<h2>Shift Scheduling with Geldimmi</h2>
<p>Geldimmi makes shift scheduling incredibly easy:</p>
<ul>
    <li><strong>Quick Employee Import:</strong> Copy-paste from Excel to add employees instantly</li>
    <li><strong>Flexible Shift Templates:</strong> Define morning, evening, night, or custom shifts</li>
    <li><strong>Drag-and-Drop Assignment:</strong> Easily assign shifts on the calendar</li>
    <li><strong>Smart Distribution:</strong> Our algorithm distributes shifts fairly</li>
</ul>

<h2>Features</h2>
<h3>📅 Monthly Calendar View</h3>
<p>See the entire month at a glance. Track who's working when instantly.</p>

<h3>🎨 Color Coding</h3>
<p>Distinguish different shift types with colors. Morning blue, night purple, etc.</p>

<h3>📱 Mobile Friendly</h3>
<p>Create and share shift schedules from your phone or tablet.</p>

<h3>📥 Excel Export</h3>
<p>Export your shift schedule to Excel with one click.</p>

<h2>Who Is It For?</h2>
<ul>
    <li>Hospitals and healthcare facilities</li>
    <li>Factories and manufacturing plants</li>
    <li>Security companies</li>
    <li>Call centers</li>
    <li>Hotels and tourism businesses</li>
    <li>Retail stores and chains</li>
</ul>
</div>",
            DisplayOrder = 1,
            IsPublished = true
        },

        // 2. Nurse Shift Planner
        new ContentPage
        {
            Slug = "nurse-shift-planner",
            Language = "en",
            Title = "Nurse Shift Planner",
            MetaDescription = "Specialized nurse scheduling software for hospitals. Fair distribution algorithm, night shift tracking, and automatic timesheet. Try free.",
            MetaKeywords = "nurse shift planner, hospital scheduling, nurse roster, healthcare scheduling, nurse duty schedule, nursing shifts",
            Subtitle = "Smart scheduling system designed for hospitals",
            CtaText = "Try Free",
            CtaUrl = "/app",
            PageType = PageType.Feature,
            Content = @"<div class='feature-content'>
<h2>A Solution Built for Nurses</h2>
<p>Nurse shift planning is one of the most challenging aspects of healthcare management. Geldimmi is specifically designed to balance nurse workloads and create a fair working environment.</p>

<h2>Challenges in Nurse Scheduling</h2>
<ul>
    <li>Fair distribution of night shifts</li>
    <li>Balancing weekend work</li>
    <li>Preventing consecutive night shifts</li>
    <li>Compliance with legal rest periods</li>
    <li>Emergency and leave management</li>
</ul>

<h2>How Geldimmi Helps</h2>
<h3>⚖️ Fair Distribution Algorithm</h3>
<p>Our smart algorithm distributes night shifts and weekend work equally among all nurses.</p>

<h3>🌙 Night Shift Tracking</h3>
<p>Automatically calculates how many night shifts each nurse has worked and reflects it in the timesheet.</p>

<h3>📊 Detailed Timesheet</h3>
<p>Calculates regular work, night work, weekends, and overtime separately.</p>

<h3>🔄 16-Hour Shift Support</h3>
<p>Supports overnight shifts like 4 PM to 8 AM commonly used in nursing.</p>

<h2>Example Nurse Schedule</h2>
<p>In a ward with 10 nurses, our system automatically:</p>
<ul>
    <li>Assigns each nurse an average of 4-5 night shifts per month</li>
    <li>Balances weekend work</li>
    <li>Prevents consecutive night shifts</li>
    <li>Considers leaves and sick days</li>
</ul>
</div>",
            DisplayOrder = 2,
            IsPublished = true
        },

        // 3. Fair Shift Distribution
        new ContentPage
        {
            Slug = "fair-shift-distribution",
            Language = "en",
            Title = "Fair Shift Distribution System",
            MetaDescription = "Smart algorithm for fair shift distribution. Balance night, weekend, and holiday shifts. Increase employee satisfaction.",
            MetaKeywords = "fair shift distribution, shift algorithm, balanced scheduling, equal shifts, shift fairness",
            Subtitle = "Balanced and fair scheduling with smart algorithm",
            CtaText = "Try Now",
            CtaUrl = "/app",
            PageType = PageType.Feature,
            Content = @"<div class='feature-content'>
<h2>Why Is Fairness Important in Shift Distribution?</h2>
<p>Unfair shift distribution can lead to employee dissatisfaction, motivation loss, and even resignations. Geldimmi's smart algorithm solves this problem.</p>

<h2>Fair Distribution Criteria</h2>
<ul>
    <li><strong>Night Shifts:</strong> Equal number of night shifts for each employee</li>
    <li><strong>Weekends:</strong> Balanced distribution of Saturday and Sunday work</li>
    <li><strong>Holidays:</strong> Fair sharing of public holiday duties</li>
    <li><strong>Total Hours:</strong> Balancing monthly work hours</li>
</ul>

<h2>How Our Algorithm Works</h2>
<h3>1. Data Collection</h3>
<p>Analyzes past shift data and employee preferences.</p>

<h3>2. Constraint Definition</h3>
<p>Considers leaves, sick days, and legal rest periods.</p>

<h3>3. Optimization</h3>
<p>Mathematical optimization is applied to find the fairest distribution.</p>

<h3>4. Balancing</h3>
<p>Night, weekend, and holiday shifts are equalized among all staff.</p>

<h2>Results</h2>
<ul>
    <li>✅ 95% fewer shift complaints</li>
    <li>✅ Increased employee satisfaction</li>
    <li>✅ Reduced manager workload</li>
    <li>✅ Transparent and measurable distribution</li>
</ul>
</div>",
            DisplayOrder = 3,
            IsPublished = true
        },

        // 4. Timesheet Calculation
        new ContentPage
        {
            Slug = "timesheet-calculation",
            Language = "en",
            Title = "Online Timesheet Calculation",
            MetaDescription = "Generate automatic timesheets from shift schedules. Calculate overtime, night work, weekends, and holidays separately. Excel export.",
            MetaKeywords = "timesheet calculation, timesheet generator, hours calculation, work hours tracking, automatic timesheet",
            Subtitle = "Automatic timesheet and work hours calculation from shift schedules",
            CtaText = "Create Timesheet",
            CtaUrl = "/app",
            PageType = PageType.Feature,
            Content = @"<div class='feature-content'>
<h2>What is a Timesheet?</h2>
<p>A timesheet is a document that shows the monthly working hours and types for each employee. It forms the basis of payroll calculation and is a legal requirement.</p>

<h2>Automatic Timesheet with Geldimmi</h2>
<p>After creating a shift schedule, get your detailed timesheet report with one click:</p>

<h3>📊 Calculated Values</h3>
<ul>
    <li><strong>Regular Work:</strong> Work within standard working hours</li>
    <li><strong>Night Work:</strong> Working hours between 8 PM and 6 AM</li>
    <li><strong>Weekend:</strong> Saturday and Sunday work</li>
    <li><strong>Public Holiday:</strong> Work on public holidays</li>
    <li><strong>Overtime:</strong> Extra work calculated daily or monthly</li>
</ul>

<h3>⚙️ Calculation Modes</h3>
<p><strong>Daily Mode:</strong> Overtime calculated separately for each day. For example, if someone who should work 8 hours works 10 hours, 2 hours overtime is recorded for that day.</p>
<p><strong>Monthly Mode:</strong> Based on total hours at month end. If monthly target is 176 hours and total work is 180 hours, 4 hours overtime is calculated.</p>

<h3>🔧 Adjustable Parameters</h3>
<ul>
    <li>Night start/end times (e.g., 8 PM - 6 AM)</li>
    <li>Daily work target (e.g., 8 hours)</li>
    <li>Monthly work target (e.g., 176 hours)</li>
    <li>Break times</li>
</ul>

<h2>Excel Export</h2>
<p>Export the generated timesheet to Excel with one click. Easily integrate with your payroll systems.</p>
</div>",
            DisplayOrder = 4,
            IsPublished = true
        },

        // 5. Overtime Calculation
        new ContentPage
        {
            Slug = "overtime-calculation",
            Language = "en",
            Title = "Overtime Calculation System",
            MetaDescription = "Daily and monthly overtime calculation. Automatic overtime tracking, legal limits, and reporting. Labor law compliant calculation.",
            MetaKeywords = "overtime calculation, overtime tracking, extra hours, overtime pay, work hours",
            Subtitle = "Automatic overtime calculation in daily or monthly mode",
            CtaText = "Start Calculating",
            CtaUrl = "/app",
            PageType = PageType.Feature,
            Content = @"<div class='feature-content'>
<h2>What is Overtime?</h2>
<p>Overtime is work that exceeds an employee's legal or contractual working hours. In most countries, work exceeding 40-45 hours per week is considered overtime.</p>

<h2>Two Different Calculation Modes</h2>
<h3>📅 Daily Calculation</h3>
<p>Overtime is calculated separately for each day:</p>
<ul>
    <li>Daily work target: 8 hours</li>
    <li>Worked today: 11 hours</li>
    <li>Overtime: 3 hours</li>
</ul>
<p><em>Advantage: Each day's overtime is clearly visible</em></p>

<h3>📆 Monthly Calculation</h3>
<p>Calculated based on total hours at month end:</p>
<ul>
    <li>Monthly work target: 176 hours</li>
    <li>Total worked: 184 hours</li>
    <li>Overtime: 8 hours</li>
</ul>
<p><em>Advantage: Extra work on some days can be balanced by less work on others</em></p>

<h2>Night Work and Overtime</h2>
<p>Example: A nurse worked a 4 PM to 8 AM shift (16 hours).</p>
<ul>
    <li>Normal work target: 8 hours/day × 2 days = 16 hours</li>
    <li>Worked: 16 hours</li>
    <li>Overtime: 0 hours</li>
    <li>Night work (8 PM - 6 AM): 10 hours</li>
</ul>
<p>In this case, there's no overtime, but 10 hours of night work premium may apply.</p>

<h2>Legal Limits</h2>
<ul>
    <li>Daily overtime: Maximum 3 hours</li>
    <li>Annual overtime: Maximum 270 hours</li>
    <li>Overtime pay: 50% more than regular rate</li>
</ul>
</div>",
            DisplayOrder = 5,
            IsPublished = true
        }
    };

    // Only add pages that don't already exist
    var pagesToAdd = contentPages
        .Where(p => !existingKeys.Contains($"{p.Slug}:{p.Language}"))
        .ToList();

    if (pagesToAdd.Any())
    {
        await context.ContentPages.AddRangeAsync(pagesToAdd);
    await context.SaveChangesAsync();
    }
}
