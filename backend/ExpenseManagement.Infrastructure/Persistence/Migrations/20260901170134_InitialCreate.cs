using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.CheckConstraint("CK_AuditLogs_AppendOnly", "[UpdatedAt] IS NULL");
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AvatarStorageKey = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.CheckConstraint("CK_Users_FullName_NotEmpty", "LEN(LTRIM([FullName])) > 0");
                    table.CheckConstraint("CK_Users_SoftDelete_Consistent", "([IsDeleted] = 0 AND [DeletedAt] IS NULL) OR ([IsDeleted] = 1 AND [DeletedAt] IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "RoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleClaims_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Type = table.Column<byte>(type: "tinyint", nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Color = table.Column<string>(type: "nchar(7)", fixedLength: true, maxLength: 7, nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.CheckConstraint("CK_Categories_Color_Hex", "[Color] LIKE '[#][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]'");
                    table.ForeignKey(
                        name: "FK_Categories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviceTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Platform = table.Column<byte>(type: "tinyint", nullable: false),
                    DeviceName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AppVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    InvalidatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceTokens", x => x.Id);
                    table.CheckConstraint("CK_DeviceTokens_Invalidated_Inactive", "[InvalidatedAt] IS NULL OR [IsActive] = 0");
                    table.ForeignKey(
                        name: "FK_DeviceTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Type = table.Column<byte>(type: "tinyint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DataJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ScheduledFor = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeliveryAttempts = table.Column<int>(type: "int", nullable: false),
                    LastDeliveryError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DeduplicationKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.CheckConstraint("CK_Notifications_DeliveryAttempts_NonNegative", "[DeliveryAttempts] >= 0");
                    table.CheckConstraint("CK_Notifications_Read_Consistent", "([IsRead] = 0 AND [ReadAt] IS NULL) OR ([IsRead] = 1 AND [ReadAt] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Notifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReplacedByTokenHash = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedByIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.CheckConstraint("CK_RefreshTokens_Revoked_Metadata", "[RevokedAt] IS NOT NULL OR ([RevokedReason] IS NULL AND [ReplacedByTokenHash] IS NULL)");
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserClaims_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_UserLogins_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Theme = table.Column<byte>(type: "tinyint", nullable: false),
                    BudgetAlertsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    RecurringRemindersEnabled = table.Column<bool>(type: "bit", nullable: false),
                    MonthlySummaryEnabled = table.Column<bool>(type: "bit", nullable: false),
                    BudgetWarningThreshold = table.Column<int>(type: "int", nullable: false),
                    BudgetCriticalThreshold = table.Column<int>(type: "int", nullable: false),
                    MonthStartDay = table.Column<int>(type: "int", nullable: false),
                    BiometricEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSettings", x => x.Id);
                    table.CheckConstraint("CK_UserSettings_BudgetCriticalThreshold_Range", "[BudgetCriticalThreshold] BETWEEN 1 AND 100");
                    table.CheckConstraint("CK_UserSettings_BudgetThresholds_Ordered", "[BudgetCriticalThreshold] >= [BudgetWarningThreshold]");
                    table.CheckConstraint("CK_UserSettings_BudgetWarningThreshold_Range", "[BudgetWarningThreshold] BETWEEN 1 AND 100");
                    table.CheckConstraint("CK_UserSettings_MonthStartDay_Range", "[MonthStartDay] BETWEEN 1 AND 28");
                    table.ForeignKey(
                        name: "FK_UserSettings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_UserTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Budgets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    Period = table.Column<byte>(type: "tinyint", nullable: false),
                    StartDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsRecurring = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    WarningThreshold = table.Column<int>(type: "int", nullable: true),
                    CriticalThreshold = table.Column<int>(type: "int", nullable: true),
                    LastNotifiedThreshold = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Budgets", x => x.Id);
                    table.CheckConstraint("CK_Budgets_Amount_Positive", "[Amount] > 0");
                    table.CheckConstraint("CK_Budgets_LastNotifiedThreshold_NonNegative", "[LastNotifiedThreshold] IS NULL OR [LastNotifiedThreshold] >= 0");
                    table.CheckConstraint("CK_Budgets_Thresholds_Ordered", "[WarningThreshold] IS NULL OR [CriticalThreshold] IS NULL OR [WarningThreshold] < [CriticalThreshold]");
                    table.CheckConstraint("CK_Budgets_Thresholds_Range", "([WarningThreshold] IS NULL OR ([WarningThreshold] BETWEEN 1 AND 100)) AND ([CriticalThreshold] IS NULL OR ([CriticalThreshold] BETWEEN 1 AND 100))");
                    table.CheckConstraint("CK_Budgets_Window_Ordered", "[EndDate] > [StartDate]");
                    table.ForeignKey(
                        name: "FK_Budgets_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Budgets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecurringTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Type = table.Column<byte>(type: "tinyint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    PaymentMethod = table.Column<byte>(type: "tinyint", nullable: false),
                    Merchant = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Frequency = table.Column<byte>(type: "tinyint", nullable: false),
                    Interval = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NextRunDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastRunDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    OccurrencesGenerated = table.Column<int>(type: "int", nullable: false),
                    IsPaused = table.Column<bool>(type: "bit", nullable: false),
                    ReminderDaysBefore = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringTransactions", x => x.Id);
                    table.CheckConstraint("CK_RecurringTransactions_Amount_Positive", "[Amount] > 0");
                    table.CheckConstraint("CK_RecurringTransactions_Interval_Positive", "[Interval] >= 1");
                    table.CheckConstraint("CK_RecurringTransactions_ReminderDaysBefore_Range", "[ReminderDaysBefore] >= 0 AND [ReminderDaysBefore] <= 30");
                    table.ForeignKey(
                        name: "FK_RecurringTransactions_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecurringTransactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<byte>(type: "tinyint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Merchant = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PaymentMethod = table.Column<byte>(type: "tinyint", nullable: false),
                    TransactionDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RecurringTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClientReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.CheckConstraint("CK_Transactions_Amount_Positive", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_Transactions_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transactions_RecurringTransactions_RecurringTransactionId",
                        column: x => x.RecurringTransactionId,
                        principalTable: "RecurringTransactions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Transactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Receipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "int", nullable: true),
                    Height = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Receipts", x => x.Id);
                    table.CheckConstraint("CK_Receipts_FileSizeBytes_Range", "[FileSizeBytes] > 0 AND [FileSizeBytes] <= 10485760");
                    table.ForeignKey(
                        name: "FK_Receipts_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Receipts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action_CreatedAt",
                table: "AuditLogs",
                columns: new[] { "Action", "CreatedAt" },
                descending: new[] { false, true })
                .Annotation("SqlServer:Include", new[] { "Succeeded", "IpAddress" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityName_EntityId_CreatedAt",
                table: "AuditLogs",
                columns: new[] { "EntityName", "EntityId", "CreatedAt" },
                descending: new[] { false, false, true },
                filter: "[EntityId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId_CreatedAt",
                table: "AuditLogs",
                columns: new[] { "UserId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_CategoryId",
                table: "Budgets",
                column: "CategoryId",
                filter: "[CategoryId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_UserId_CategoryId_StartDate",
                table: "Budgets",
                columns: new[] { "UserId", "CategoryId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_UserId_EndDate_Active",
                table: "Budgets",
                columns: new[] { "UserId", "EndDate" },
                filter: "[IsActive] = 1 AND [IsDeleted] = 0")
                .Annotation("SqlServer:Include", new[] { "CategoryId", "Amount", "LastNotifiedThreshold" });

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_UserId_StartDate_EndDate",
                table: "Budgets",
                columns: new[] { "UserId", "StartDate", "EndDate" })
                .Annotation("SqlServer:Include", new[] { "Amount", "CategoryId", "IsActive", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "UX_Budgets_UserId_CategoryId_Period_StartDate",
                table: "Budgets",
                columns: new[] { "UserId", "CategoryId", "Period", "StartDate" },
                unique: true,
                filter: "[CategoryId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UX_Budgets_UserId_Period_StartDate_Overall",
                table: "Budgets",
                columns: new[] { "UserId", "Period", "StartDate" },
                unique: true,
                filter: "[CategoryId] IS NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_UserId_Type_SortOrder",
                table: "Categories",
                columns: new[] { "UserId", "Type", "SortOrder" })
                .Annotation("SqlServer:Include", new[] { "Name", "Icon", "Color", "IsSystem", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "UX_Categories_UserId_Name_Type",
                table: "Categories",
                columns: new[] { "UserId", "Name", "Type" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTokens_LastSeenAt",
                table: "DeviceTokens",
                column: "LastSeenAt",
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTokens_UserId_IsActive",
                table: "DeviceTokens",
                columns: new[] { "UserId", "IsActive" })
                .Annotation("SqlServer:Include", new[] { "Token", "Platform" });

            migrationBuilder.CreateIndex(
                name: "UX_DeviceTokens_Token",
                table: "DeviceTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ScheduledFor",
                table: "Notifications",
                column: "ScheduledFor",
                filter: "[SentAt] IS NULL AND [IsDeleted] = 0")
                .Annotation("SqlServer:Include", new[] { "UserId", "Type", "DeliveryAttempts" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead", "CreatedAt" },
                descending: new[] { false, false, true })
                .Annotation("SqlServer:Include", new[] { "Title", "Type", "SentAt", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "UX_Notifications_UserId_DeduplicationKey",
                table: "Notifications",
                columns: new[] { "UserId", "DeduplicationKey" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_TransactionId",
                table: "Receipts",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_UserId_TransactionId",
                table: "Receipts",
                columns: new[] { "UserId", "TransactionId" })
                .Annotation("SqlServer:Include", new[] { "FileName", "ContentType", "FileSizeBytes", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "UX_Receipts_StorageKey",
                table: "Receipts",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTransactions_CategoryId",
                table: "RecurringTransactions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTransactions_NextRunDate",
                table: "RecurringTransactions",
                column: "NextRunDate",
                filter: "[IsPaused] = 0 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTransactions_UserId_IsPaused",
                table: "RecurringTransactions",
                columns: new[] { "UserId", "IsPaused" })
                .Annotation("SqlServer:Include", new[] { "Name", "Amount", "NextRunDate", "Frequency", "CategoryId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_FamilyId",
                table: "RefreshTokens",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId_ExpiresAt",
                table: "RefreshTokens",
                columns: new[] { "UserId", "ExpiresAt" })
                .Annotation("SqlServer:Include", new[] { "RevokedAt", "FamilyId" });

            migrationBuilder.CreateIndex(
                name: "UX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleClaims_RoleId",
                table: "RoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "Roles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CategoryId",
                table: "Transactions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_RecurringTransactionId",
                table: "Transactions",
                column: "RecurringTransactionId",
                filter: "[RecurringTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_UserId_CategoryId_TransactionDate",
                table: "Transactions",
                columns: new[] { "UserId", "CategoryId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_UserId_TransactionDate",
                table: "Transactions",
                columns: new[] { "UserId", "TransactionDate" },
                descending: new[] { false, true })
                .Annotation("SqlServer:Include", new[] { "Amount", "Type", "CategoryId", "Merchant", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_UserId_Type_TransactionDate",
                table: "Transactions",
                columns: new[] { "UserId", "Type", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "UX_Transactions_UserId_ClientReference",
                table: "Transactions",
                columns: new[] { "UserId", "ClientReference" },
                unique: true,
                filter: "[ClientReference] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_UserClaims_UserId",
                table: "UserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLogins_UserId",
                table: "UserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "Users",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Users_DeletedAt",
                table: "Users",
                column: "DeletedAt",
                filter: "[IsDeleted] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Users_LastLoginAt",
                table: "Users",
                column: "LastLoginAt",
                filter: "[IsDeleted] = 0")
                .Annotation("SqlServer:Include", new[] { "FullName", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "Users",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_UserSettings_UserId",
                table: "UserSettings",
                column: "UserId",
                unique: true)
                .Annotation("SqlServer:Include", new[] { "CurrencyCode", "Locale", "TimeZoneId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "Budgets");

            migrationBuilder.DropTable(
                name: "DeviceTokens");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "Receipts");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "RoleClaims");

            migrationBuilder.DropTable(
                name: "UserClaims");

            migrationBuilder.DropTable(
                name: "UserLogins");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "UserSettings");

            migrationBuilder.DropTable(
                name: "UserTokens");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "RecurringTransactions");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
