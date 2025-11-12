using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyKeyVault.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddTushareModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BalanceSheets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TsCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AnnounceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FReportDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReportType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CompType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TotalAssets = table.Column<decimal>(type: "numeric", nullable: true),
                    TotalLiab = table.Column<decimal>(type: "numeric", nullable: true),
                    TotalHldrEqyExcMinInt = table.Column<decimal>(type: "numeric", nullable: true),
                    TotalHldrEqyIncMinInt = table.Column<decimal>(type: "numeric", nullable: true),
                    TotalCurAssets = table.Column<decimal>(type: "numeric", nullable: true),
                    MoneyFund = table.Column<decimal>(type: "numeric", nullable: true),
                    TradeFinAssets = table.Column<decimal>(type: "numeric", nullable: true),
                    NotesReceiv = table.Column<decimal>(type: "numeric", nullable: true),
                    AccountsReceiv = table.Column<decimal>(type: "numeric", nullable: true),
                    Inventory = table.Column<decimal>(type: "numeric", nullable: true),
                    TotalNcaAssets = table.Column<decimal>(type: "numeric", nullable: true),
                    FixAssets = table.Column<decimal>(type: "numeric", nullable: true),
                    InvestRealEstate = table.Column<decimal>(type: "numeric", nullable: true),
                    GoodWill = table.Column<decimal>(type: "numeric", nullable: true),
                    IntangAssets = table.Column<decimal>(type: "numeric", nullable: true),
                    TotalCurLiab = table.Column<decimal>(type: "numeric", nullable: true),
                    ShortLoan = table.Column<decimal>(type: "numeric", nullable: true),
                    AccountsPayable = table.Column<decimal>(type: "numeric", nullable: true),
                    TotalNcaLiab = table.Column<decimal>(type: "numeric", nullable: true),
                    LongLoan = table.Column<decimal>(type: "numeric", nullable: true),
                    BondPayable = table.Column<decimal>(type: "numeric", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BalanceSheets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CallLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AppId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ApiName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ParamsHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ParamsJson = table.Column<string>(type: "text", nullable: false),
                    RequestAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: false),
                    RequestId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CallLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CashflowStatements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TsCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AnnounceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FReportDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReportType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CompType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    NetCashOperAct = table.Column<decimal>(type: "numeric", nullable: true),
                    CashRecSg = table.Column<decimal>(type: "numeric", nullable: true),
                    CashPayGoods = table.Column<decimal>(type: "numeric", nullable: true),
                    CashPayEmp = table.Column<decimal>(type: "numeric", nullable: true),
                    PaidAllTax = table.Column<decimal>(type: "numeric", nullable: true),
                    NetCashInvAct = table.Column<decimal>(type: "numeric", nullable: true),
                    CashRecInvest = table.Column<decimal>(type: "numeric", nullable: true),
                    InvestPayCash = table.Column<decimal>(type: "numeric", nullable: true),
                    NetCashFixAssets = table.Column<decimal>(type: "numeric", nullable: true),
                    NetCashFinAct = table.Column<decimal>(type: "numeric", nullable: true),
                    CashRecCap = table.Column<decimal>(type: "numeric", nullable: true),
                    CashRecBorrow = table.Column<decimal>(type: "numeric", nullable: true),
                    CashPayDist = table.Column<decimal>(type: "numeric", nullable: true),
                    CashPayDebts = table.Column<decimal>(type: "numeric", nullable: true),
                    NCashIncr = table.Column<decimal>(type: "numeric", nullable: true),
                    CashBegin = table.Column<decimal>(type: "numeric", nullable: true),
                    CashEnd = table.Column<decimal>(type: "numeric", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashflowStatements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IncomeStatements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TsCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AnnounceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FReportDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReportType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CompType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TotalRevenue = table.Column<decimal>(type: "numeric", nullable: true),
                    Revenue = table.Column<decimal>(type: "numeric", nullable: true),
                    OperateProfit = table.Column<decimal>(type: "numeric", nullable: true),
                    TotalProfit = table.Column<decimal>(type: "numeric", nullable: true),
                    NIncomeAttrP = table.Column<decimal>(type: "numeric", nullable: true),
                    NetProfit = table.Column<decimal>(type: "numeric", nullable: true),
                    BasicEps = table.Column<decimal>(type: "numeric", nullable: true),
                    DilutedEps = table.Column<decimal>(type: "numeric", nullable: true),
                    TotalCogs = table.Column<decimal>(type: "numeric", nullable: true),
                    OperateCost = table.Column<decimal>(type: "numeric", nullable: true),
                    SellExp = table.Column<decimal>(type: "numeric", nullable: true),
                    AdminExp = table.Column<decimal>(type: "numeric", nullable: true),
                    FinExp = table.Column<decimal>(type: "numeric", nullable: true),
                    RdExp = table.Column<decimal>(type: "numeric", nullable: true),
                    ImpairLoss = table.Column<decimal>(type: "numeric", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomeStatements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockBasics",
                columns: table => new
                {
                    TsCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Area = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Industry = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Fullname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EnName = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Market = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Exchange = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CurrType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ListDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DelistDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsHs = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockBasics", x => x.TsCode);
                });

            migrationBuilder.CreateTable(
                name: "StockDailies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TsCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TradeDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Open = table.Column<decimal>(type: "numeric", nullable: true),
                    High = table.Column<decimal>(type: "numeric", nullable: true),
                    Low = table.Column<decimal>(type: "numeric", nullable: true),
                    Close = table.Column<decimal>(type: "numeric", nullable: true),
                    PreClose = table.Column<decimal>(type: "numeric", nullable: true),
                    Change = table.Column<decimal>(type: "numeric", nullable: true),
                    PctChg = table.Column<decimal>(type: "numeric", nullable: true),
                    Vol = table.Column<decimal>(type: "numeric", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric", nullable: true),
                    SourceUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockDailies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TushareApps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AppId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EncryptedSecret = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TushareApps", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BalanceSheets_EndDate",
                table: "BalanceSheets",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_BalanceSheets_TsCode",
                table: "BalanceSheets",
                column: "TsCode");

            migrationBuilder.CreateIndex(
                name: "IX_BalanceSheets_TsCode_EndDate",
                table: "BalanceSheets",
                columns: new[] { "TsCode", "EndDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CallLogs_ApiName",
                table: "CallLogs",
                column: "ApiName");

            migrationBuilder.CreateIndex(
                name: "IX_CallLogs_AppId_RequestAt",
                table: "CallLogs",
                columns: new[] { "AppId", "RequestAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CashflowStatements_EndDate",
                table: "CashflowStatements",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_CashflowStatements_TsCode",
                table: "CashflowStatements",
                column: "TsCode");

            migrationBuilder.CreateIndex(
                name: "IX_CashflowStatements_TsCode_EndDate",
                table: "CashflowStatements",
                columns: new[] { "TsCode", "EndDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncomeStatements_EndDate",
                table: "IncomeStatements",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeStatements_TsCode",
                table: "IncomeStatements",
                column: "TsCode");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeStatements_TsCode_EndDate",
                table: "IncomeStatements",
                columns: new[] { "TsCode", "EndDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockDailies_TradeDate",
                table: "StockDailies",
                column: "TradeDate");

            migrationBuilder.CreateIndex(
                name: "IX_StockDailies_TsCode",
                table: "StockDailies",
                column: "TsCode");

            migrationBuilder.CreateIndex(
                name: "IX_StockDailies_TsCode_TradeDate",
                table: "StockDailies",
                columns: new[] { "TsCode", "TradeDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TushareApps_AppId",
                table: "TushareApps",
                column: "AppId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TushareApps_UserId",
                table: "TushareApps",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BalanceSheets");

            migrationBuilder.DropTable(
                name: "CallLogs");

            migrationBuilder.DropTable(
                name: "CashflowStatements");

            migrationBuilder.DropTable(
                name: "IncomeStatements");

            migrationBuilder.DropTable(
                name: "StockBasics");

            migrationBuilder.DropTable(
                name: "StockDailies");

            migrationBuilder.DropTable(
                name: "TushareApps");
        }
    }
}
