using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountsAndTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                table: "Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<byte>(type: "smallint", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Institution = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Last4 = table.Column<string>(type: "character(4)", fixedLength: true, maxLength: 4, nullable: true),
                    Icon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Color = table.Column<string>(type: "character(7)", fixedLength: true, maxLength: 7, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                    table.CheckConstraint("CK_Accounts_Color_Hex", "\"Color\" ~ '^#[0-9A-Fa-f]{6}$'");
                    table.CheckConstraint("CK_Accounts_CurrencyCode_Iso", "\"CurrencyCode\" ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("CK_Accounts_Last4_Digits", "\"Last4\" IS NULL OR \"Last4\" ~ '^[0-9]{4}$'");
                    table.ForeignKey(
                        name: "FK_Accounts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Transfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    FromAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    TransferDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ClientReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transfers", x => x.Id);
                    table.CheckConstraint("CK_Transfers_Accounts_Differ", "\"FromAccountId\" <> \"ToAccountId\"");
                    table.CheckConstraint("CK_Transfers_Amount_Positive", "\"Amount\" > 0");
                    table.CheckConstraint("CK_Transfers_CurrencyCode_Iso", "\"CurrencyCode\" ~ '^[A-Z]{3}$'");
                    table.ForeignKey(
                        name: "FK_Transfers_Accounts_FromAccountId",
                        column: x => x.FromAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Transfers_Accounts_ToAccountId",
                        column: x => x.ToAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Transfers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_AccountId_TransactionDate",
                table: "Transactions",
                columns: new[] { "AccountId", "TransactionDate" })
                .Annotation("Npgsql:IndexInclude", new[] { "Amount", "Type", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_UserId_SortOrder",
                table: "Accounts",
                columns: new[] { "UserId", "SortOrder" })
                .Annotation("Npgsql:IndexInclude", new[] { "Name", "Type", "Icon", "Color", "IsArchived", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "UX_Accounts_UserId_Default",
                table: "Accounts",
                column: "UserId",
                unique: true,
                filter: "\"IsDefault\" = true AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "UX_Accounts_UserId_Name",
                table: "Accounts",
                columns: new[] { "UserId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_FromAccountId_TransferDate",
                table: "Transfers",
                columns: new[] { "FromAccountId", "TransferDate" })
                .Annotation("Npgsql:IndexInclude", new[] { "Amount", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_ToAccountId_TransferDate",
                table: "Transfers",
                columns: new[] { "ToAccountId", "TransferDate" })
                .Annotation("Npgsql:IndexInclude", new[] { "Amount", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_UserId_TransferDate",
                table: "Transfers",
                columns: new[] { "UserId", "TransferDate" },
                descending: new[] { false, true })
                .Annotation("Npgsql:IndexInclude", new[] { "FromAccountId", "ToAccountId", "Amount", "CurrencyCode", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "UX_Transfers_UserId_ClientReference",
                table: "Transfers",
                columns: new[] { "UserId", "ClientReference" },
                unique: true,
                filter: "\"ClientReference\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Accounts_AccountId",
                table: "Transactions",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
            // EF Core has no fluent syntax for an index over an expression, so
            // this one is hand-written and is absent from the model snapshot.
            //
            // UX_Accounts_UserId_Name above is case-SENSITIVE on PostgreSQL,
            // so "Wallet" and "wallet" would both be accepted as distinct
            // accounts. AccountService rejects the duplicate, but a check in
            // the service and a constraint in the database have to agree or two
            // concurrent requests slip past both. This closes that race.
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "UX_Accounts_UserId_NameLower"
                    ON "Accounts" ("UserId", lower("Name"))
                    WHERE "IsDeleted" = false;
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Accounts_AccountId",
                table: "Transactions");

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "UX_Accounts_UserId_NameLower";
                """);

            migrationBuilder.DropTable(
                name: "Transfers");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_AccountId_TransactionDate",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "Transactions");
        }
    }
}
