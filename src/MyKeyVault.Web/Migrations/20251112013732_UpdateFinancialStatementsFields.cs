using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyKeyVault.Web.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFinancialStatementsFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnnounceDate",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "FReportDate",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "AnnounceDate",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "FReportDate",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "AnnounceDate",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "FReportDate",
                table: "BalanceSheets");

            migrationBuilder.RenameColumn(
                name: "OperateCost",
                table: "IncomeStatements",
                newName: "WorkersWelfare");

            migrationBuilder.RenameColumn(
                name: "NetProfit",
                table: "IncomeStatements",
                newName: "WithdraReseFund");

            migrationBuilder.RenameColumn(
                name: "ImpairLoss",
                table: "IncomeStatements",
                newName: "WithdraOthErsu");

            migrationBuilder.RenameColumn(
                name: "PaidAllTax",
                table: "CashflowStatements",
                newName: "UseRightAssetDep");

            migrationBuilder.RenameColumn(
                name: "NetCashOperAct",
                table: "CashflowStatements",
                newName: "UnconInvestLoss");

            migrationBuilder.RenameColumn(
                name: "NetCashInvAct",
                table: "CashflowStatements",
                newName: "StotOutInvAct");

            migrationBuilder.RenameColumn(
                name: "NetCashFixAssets",
                table: "CashflowStatements",
                newName: "StotInflowsInvAct");

            migrationBuilder.RenameColumn(
                name: "NetCashFinAct",
                table: "CashflowStatements",
                newName: "StotCashoutFncAct");

            migrationBuilder.RenameColumn(
                name: "NCashIncr",
                table: "CashflowStatements",
                newName: "StotCashInFncAct");

            migrationBuilder.RenameColumn(
                name: "InvestPayCash",
                table: "CashflowStatements",
                newName: "StCashOutAct");

            migrationBuilder.RenameColumn(
                name: "CashRecSg",
                table: "CashflowStatements",
                newName: "RecpTaxRends");

            migrationBuilder.RenameColumn(
                name: "CashRecInvest",
                table: "CashflowStatements",
                newName: "ProvDeprAssets");

            migrationBuilder.RenameColumn(
                name: "CashRecCap",
                table: "CashflowStatements",
                newName: "ProcIssueBonds");

            migrationBuilder.RenameColumn(
                name: "CashRecBorrow",
                table: "CashflowStatements",
                newName: "PremFrOrigContr");

            migrationBuilder.RenameColumn(
                name: "CashPayGoods",
                table: "CashflowStatements",
                newName: "PayHandlingChrg");

            migrationBuilder.RenameColumn(
                name: "CashPayEmp",
                table: "CashflowStatements",
                newName: "PayCommInsurPlcy");

            migrationBuilder.RenameColumn(
                name: "CashPayDist",
                table: "CashflowStatements",
                newName: "Others");

            migrationBuilder.RenameColumn(
                name: "CashPayDebts",
                table: "CashflowStatements",
                newName: "OthRecpRalInvAct");

            migrationBuilder.RenameColumn(
                name: "CashEnd",
                table: "CashflowStatements",
                newName: "OthPayRalInvAct");

            migrationBuilder.RenameColumn(
                name: "CashBegin",
                table: "CashflowStatements",
                newName: "OthLossAsset");

            migrationBuilder.RenameColumn(
                name: "GoodWill",
                table: "BalanceSheets",
                newName: "Goodwill");

            migrationBuilder.RenameColumn(
                name: "TradeFinAssets",
                table: "BalanceSheets",
                newName: "UseRightAssets");

            migrationBuilder.RenameColumn(
                name: "TotalNcaLiab",
                table: "BalanceSheets",
                newName: "UndistrPorfit");

            migrationBuilder.RenameColumn(
                name: "TotalNcaAssets",
                table: "BalanceSheets",
                newName: "TreasuryShare");

            migrationBuilder.RenameColumn(
                name: "ShortLoan",
                table: "BalanceSheets",
                newName: "TransacSeatFee");

            migrationBuilder.RenameColumn(
                name: "MoneyFund",
                table: "BalanceSheets",
                newName: "TradingFl");

            migrationBuilder.RenameColumn(
                name: "LongLoan",
                table: "BalanceSheets",
                newName: "TradAsset");

            migrationBuilder.RenameColumn(
                name: "Inventory",
                table: "BalanceSheets",
                newName: "TotalShare");

            migrationBuilder.RenameColumn(
                name: "IntangAssets",
                table: "BalanceSheets",
                newName: "TotalNcl");

            migrationBuilder.RenameColumn(
                name: "AccountsPayable",
                table: "BalanceSheets",
                newName: "TotalNca");

            migrationBuilder.AlterColumn<string>(
                name: "ReportType",
                table: "IncomeStatements",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CompType",
                table: "IncomeStatements",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdjLossgain",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AmodcostFinAssets",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnnDate",
                table: "IncomeStatements",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AssInvestIncome",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AssetDispIncome",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AssetsImpairLoss",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BizTaxSurchg",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CapitComstockDiv",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommExp",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommIncome",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CompensPayout",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CompensPayoutRefu",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ComprIncAttrMS",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ComprIncAttrP",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ComsharePayableDvd",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ContinuedNetProfit",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditImpaLoss",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DistableProfit",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DistrProfitShrhder",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DivPayt",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Ebit",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Ebitda",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EndNetProfit",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EndType",
                table: "IncomeStatements",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FAnnDate",
                table: "IncomeStatements",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinExpIntExp",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinExpIntInc",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ForexGain",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FvValueChgGain",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IncomeTax",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InsurReserRefu",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InsuranceExp",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IntExp",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IntIncome",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InvestIncome",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinorityGain",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NAssetMgIncome",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NCommisIncome",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NIncome",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NOthBIncome",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NOthIncome",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NSecTbIncome",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NSecUwIncome",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NcaDisploss",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NetAfterNrLpCorrect",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NetExpoHedgingBenefits",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NonOperExp",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NonOperIncome",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OperCost",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OperExp",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OthBIncome",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OthComprIncome",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OthImpairLossAssets",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OthIncome",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OtherBusCost",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OutPrem",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PremEarned",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PremIncome",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PremRefund",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PrfsharePayableDvd",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReinsCostRefund",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReinsExp",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReinsIncome",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReserInsurLiab",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TComprIncome",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalOpcost",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TransferHousingImprest",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TransferOth",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TransferSurplusRese",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UndistProfit",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnePremReser",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdateFlag",
                table: "IncomeStatements",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WithdraBizDevfund",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WithdraLegalPubfund",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WithdraLegalSurplus",
                table: "IncomeStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReportType",
                table: "CashflowStatements",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CompType",
                table: "CashflowStatements",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AmortIntangAssets",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnnDate",
                table: "CashflowStatements",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BegBalCash",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BegBalCashEqu",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CCashEquBegPeriod",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CCashEquEndPeriod",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CDispWithdrwlInvest",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CFrOthOperateA",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CFrSaleSg",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CInfFrOperateA",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CPaidForTaxes",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CPaidGoodsS",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CPaidInvest",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CPaidToForEmpl",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CPayAcqConstFilta",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CPayClaimsOrigInco",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CPayDistDpcpIntExp",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CPrepayAmtBorr",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CRecpBorrow",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CRecpCapContrib",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CRecpReturnInvest",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ConvCopbondsDueWithin1y",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ConvDebtIntoCap",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditImpaLoss",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DecrDefIncTaxAssets",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DecrDeferredExp",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DecrInventories",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DecrOperPayable",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeprFaCogaDpba",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EffFxFluCash",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EndBalCash",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EndBalCashEqu",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EndType",
                table: "CashflowStatements",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FAnnDate",
                table: "CashflowStatements",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FaFncLeases",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinanExp",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FreeCashflow",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IfcCashIncr",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ImNIncrCashEqu",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ImNetCashflowOperAct",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InclCashRecSaims",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InclDvdProfitPaidScMs",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IncrAccExp",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IncrDefIncTaxLiab",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IncrOperPayable",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InvestLoss",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LossDispFilta",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LossFvChg",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LossScrFa",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LtAmortDeferredExp",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NCapIncrRepur",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NCashFlowsFncAct",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NCashflowAct",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NCashflowInvAct",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NDeposIncrFi",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NDispSubsOthBiz",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NIncBorrOthFi",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NIncrCashCashEqu",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NIncrCltLoanAdv",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NIncrDepCbob",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NIncrDispFaas",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NIncrDispTfa",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NIncrInsuredDep",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NIncrLoansCb",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NIncrLoansOthBank",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NIncrPledgeLoan",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NRecpDispFilta",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NRecpDispSobu",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NReinsurPrem",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NetCashReceSec",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NetDismCapitalAdd",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NetProfit",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OthCashPayOperAct",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OthCashRecpRalFncAct",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OthCashpayRalFncAct",
                table: "CashflowStatements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdateFlag",
                table: "CashflowStatements",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReportType",
                table: "BalanceSheets",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CompType",
                table: "BalanceSheets",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AccExp",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AccReceivable",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AccountsPay",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AccountsReceivBill",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AcctPayable",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ActingTradingSec",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ActingUwSec",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdvReceipts",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AgencyBusLiab",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AmorExp",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnnDate",
                table: "BalanceSheets",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CapRese",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CashReserCb",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CbBorr",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Cip",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CipTotal",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ClientDepos",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ClientProv",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommPayable",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ConstMaterials",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ContractAssets",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ContractLiab",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostFinAssets",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DebtInvest",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DecrInDisbur",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeferIncNonCurLiab",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeferTaxAssets",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeferTaxLiab",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeferredInc",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Depos",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeposIbDeposits",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeposInOthBfi",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeposOthBfi",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeposReceived",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DerivAssets",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DerivLiab",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DivPayable",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DivReceiv",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EndType",
                table: "BalanceSheets",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedLiab",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FAnnDate",
                table: "BalanceSheets",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FaAvailForSale",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FairValueFinAssets",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FixAssetsTotal",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FixedAssetsDisp",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ForexDiffer",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HfsAssets",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HfsSales",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HtmInvest",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IndemPayable",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IndepAcctAssets",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IndeptAccLiab",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IntPayable",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IntReceiv",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IntanAssets",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Inventories",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InvestAsReceiv",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InvestLossUnconf",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LeaseLiab",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LendingFunds",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LoanOthBank",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LoantoOthBankFi",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LongPayTotal",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LtAmorExp",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LtBorr",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LtEqtInvest",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LtPayable",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LtPayrollPayable",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LtRec",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinorityInt",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MoneyCap",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NcaWithin1y",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NonCurLiabDue1y",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NotesPayable",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OilAndGasAssets",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OrdinRiskReser",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OthAssets",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OthCompIncome",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OthCurAssets",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OthCurLiab",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OthDebtInvest",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OthEqInvest",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OthEqPpbond",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OthEqtTools",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OthEqtToolsPShr",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OthIlliqFinAssets",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OthLiab",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OthNca",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OthNcl",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OthPayTotal",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OthPayable",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OthRcvTotal",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OthReceiv",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PayableToReinsurer",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Payables",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PayrollPayable",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PhInvest",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PhPledgeLoans",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PledgeBorr",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PolicyDivPayable",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PrecMetals",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PremReceivAdva",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PremiumReceiv",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Prepayment",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProducBioAssets",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PurResaleFa",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RAndD",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReceivFinancing",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundCapDepos",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundDepos",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReinsurReceiv",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReinsurResReceiv",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReserLinsLiab",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReserLthinsLiab",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReserOutstdClaims",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReserUnePrem",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RrReinsLinsLiab",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RrReinsLthinsLiab",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RrReinsOutstdCla",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RrReinsUnePrem",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RsrvInsurCont",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SettRsrv",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SoldForRepurFa",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SpecialRese",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SpecificPayables",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StBondsPayable",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StBorr",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StFinPayable",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SurplusRese",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxesPayable",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TimeDeposits",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalLiabHldrEqy",
                table: "BalanceSheets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdateFlag",
                table: "BalanceSheets",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdjLossgain",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "AmodcostFinAssets",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "AnnDate",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "AssInvestIncome",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "AssetDispIncome",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "AssetsImpairLoss",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "BizTaxSurchg",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "CapitComstockDiv",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "CommExp",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "CommIncome",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "CompensPayout",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "CompensPayoutRefu",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "ComprIncAttrMS",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "ComprIncAttrP",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "ComsharePayableDvd",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "ContinuedNetProfit",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "CreditImpaLoss",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "DistableProfit",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "DistrProfitShrhder",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "DivPayt",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "Ebit",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "Ebitda",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "EndNetProfit",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "EndType",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "FAnnDate",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "FinExpIntExp",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "FinExpIntInc",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "ForexGain",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "FvValueChgGain",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "IncomeTax",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "InsurReserRefu",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "InsuranceExp",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "IntExp",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "IntIncome",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "InvestIncome",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "MinorityGain",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "NAssetMgIncome",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "NCommisIncome",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "NIncome",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "NOthBIncome",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "NOthIncome",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "NSecTbIncome",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "NSecUwIncome",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "NcaDisploss",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "NetAfterNrLpCorrect",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "NetExpoHedgingBenefits",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "NonOperExp",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "NonOperIncome",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "OperCost",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "OperExp",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "OthBIncome",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "OthComprIncome",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "OthImpairLossAssets",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "OthIncome",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "OtherBusCost",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "OutPrem",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "PremEarned",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "PremIncome",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "PremRefund",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "PrfsharePayableDvd",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "ReinsCostRefund",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "ReinsExp",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "ReinsIncome",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "ReserInsurLiab",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "TComprIncome",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "TotalOpcost",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "TransferHousingImprest",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "TransferOth",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "TransferSurplusRese",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "UndistProfit",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "UnePremReser",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "UpdateFlag",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "WithdraBizDevfund",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "WithdraLegalPubfund",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "WithdraLegalSurplus",
                table: "IncomeStatements");

            migrationBuilder.DropColumn(
                name: "AmortIntangAssets",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "AnnDate",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "BegBalCash",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "BegBalCashEqu",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "CCashEquBegPeriod",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "CCashEquEndPeriod",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "CDispWithdrwlInvest",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "CFrOthOperateA",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "CFrSaleSg",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "CInfFrOperateA",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "CPaidForTaxes",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "CPaidGoodsS",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "CPaidInvest",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "CPaidToForEmpl",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "CPayAcqConstFilta",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "CPayClaimsOrigInco",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "CPayDistDpcpIntExp",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "CPrepayAmtBorr",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "CRecpBorrow",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "CRecpCapContrib",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "CRecpReturnInvest",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "ConvCopbondsDueWithin1y",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "ConvDebtIntoCap",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "CreditImpaLoss",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "DecrDefIncTaxAssets",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "DecrDeferredExp",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "DecrInventories",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "DecrOperPayable",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "DeprFaCogaDpba",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "EffFxFluCash",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "EndBalCash",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "EndBalCashEqu",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "EndType",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "FAnnDate",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "FaFncLeases",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "FinanExp",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "FreeCashflow",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "IfcCashIncr",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "ImNIncrCashEqu",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "ImNetCashflowOperAct",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "InclCashRecSaims",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "InclDvdProfitPaidScMs",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "IncrAccExp",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "IncrDefIncTaxLiab",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "IncrOperPayable",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "InvestLoss",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "LossDispFilta",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "LossFvChg",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "LossScrFa",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "LtAmortDeferredExp",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "NCapIncrRepur",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "NCashFlowsFncAct",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "NCashflowAct",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "NCashflowInvAct",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "NDeposIncrFi",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "NDispSubsOthBiz",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "NIncBorrOthFi",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "NIncrCashCashEqu",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "NIncrCltLoanAdv",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "NIncrDepCbob",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "NIncrDispFaas",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "NIncrDispTfa",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "NIncrInsuredDep",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "NIncrLoansCb",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "NIncrLoansOthBank",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "NIncrPledgeLoan",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "NRecpDispFilta",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "NRecpDispSobu",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "NReinsurPrem",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "NetCashReceSec",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "NetDismCapitalAdd",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "NetProfit",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "OthCashPayOperAct",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "OthCashRecpRalFncAct",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "OthCashpayRalFncAct",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "UpdateFlag",
                table: "CashflowStatements");

            migrationBuilder.DropColumn(
                name: "AccExp",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "AccReceivable",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "AccountsPay",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "AccountsReceivBill",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "AcctPayable",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "ActingTradingSec",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "ActingUwSec",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "AdvReceipts",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "AgencyBusLiab",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "AmorExp",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "AnnDate",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "CapRese",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "CashReserCb",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "CbBorr",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "Cip",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "CipTotal",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "ClientDepos",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "ClientProv",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "CommPayable",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "ConstMaterials",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "ContractAssets",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "ContractLiab",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "CostFinAssets",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "DebtInvest",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "DecrInDisbur",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "DeferIncNonCurLiab",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "DeferTaxAssets",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "DeferTaxLiab",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "DeferredInc",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "Depos",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "DeposIbDeposits",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "DeposInOthBfi",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "DeposOthBfi",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "DeposReceived",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "DerivAssets",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "DerivLiab",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "DivPayable",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "DivReceiv",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "EndType",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "EstimatedLiab",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "FAnnDate",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "FaAvailForSale",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "FairValueFinAssets",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "FixAssetsTotal",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "FixedAssetsDisp",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "ForexDiffer",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "HfsAssets",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "HfsSales",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "HtmInvest",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "IndemPayable",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "IndepAcctAssets",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "IndeptAccLiab",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "IntPayable",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "IntReceiv",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "IntanAssets",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "Inventories",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "InvestAsReceiv",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "InvestLossUnconf",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "LeaseLiab",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "LendingFunds",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "LoanOthBank",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "LoantoOthBankFi",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "LongPayTotal",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "LtAmorExp",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "LtBorr",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "LtEqtInvest",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "LtPayable",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "LtPayrollPayable",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "LtRec",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "MinorityInt",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "MoneyCap",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "NcaWithin1y",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "NonCurLiabDue1y",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "NotesPayable",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "OilAndGasAssets",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "OrdinRiskReser",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "OthAssets",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "OthCompIncome",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "OthCurAssets",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "OthCurLiab",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "OthDebtInvest",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "OthEqInvest",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "OthEqPpbond",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "OthEqtTools",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "OthEqtToolsPShr",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "OthIlliqFinAssets",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "OthLiab",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "OthNca",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "OthNcl",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "OthPayTotal",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "OthPayable",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "OthRcvTotal",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "OthReceiv",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "PayableToReinsurer",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "Payables",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "PayrollPayable",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "PhInvest",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "PhPledgeLoans",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "PledgeBorr",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "PolicyDivPayable",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "PrecMetals",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "PremReceivAdva",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "PremiumReceiv",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "Prepayment",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "ProducBioAssets",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "PurResaleFa",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "RAndD",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "ReceivFinancing",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "RefundCapDepos",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "RefundDepos",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "ReinsurReceiv",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "ReinsurResReceiv",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "ReserLinsLiab",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "ReserLthinsLiab",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "ReserOutstdClaims",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "ReserUnePrem",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "RrReinsLinsLiab",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "RrReinsLthinsLiab",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "RrReinsOutstdCla",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "RrReinsUnePrem",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "RsrvInsurCont",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "SettRsrv",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "SoldForRepurFa",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "SpecialRese",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "SpecificPayables",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "StBondsPayable",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "StBorr",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "StFinPayable",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "SurplusRese",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "TaxesPayable",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "TimeDeposits",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "TotalLiabHldrEqy",
                table: "BalanceSheets");

            migrationBuilder.DropColumn(
                name: "UpdateFlag",
                table: "BalanceSheets");

            migrationBuilder.RenameColumn(
                name: "WorkersWelfare",
                table: "IncomeStatements",
                newName: "OperateCost");

            migrationBuilder.RenameColumn(
                name: "WithdraReseFund",
                table: "IncomeStatements",
                newName: "NetProfit");

            migrationBuilder.RenameColumn(
                name: "WithdraOthErsu",
                table: "IncomeStatements",
                newName: "ImpairLoss");

            migrationBuilder.RenameColumn(
                name: "UseRightAssetDep",
                table: "CashflowStatements",
                newName: "PaidAllTax");

            migrationBuilder.RenameColumn(
                name: "UnconInvestLoss",
                table: "CashflowStatements",
                newName: "NetCashOperAct");

            migrationBuilder.RenameColumn(
                name: "StotOutInvAct",
                table: "CashflowStatements",
                newName: "NetCashInvAct");

            migrationBuilder.RenameColumn(
                name: "StotInflowsInvAct",
                table: "CashflowStatements",
                newName: "NetCashFixAssets");

            migrationBuilder.RenameColumn(
                name: "StotCashoutFncAct",
                table: "CashflowStatements",
                newName: "NetCashFinAct");

            migrationBuilder.RenameColumn(
                name: "StotCashInFncAct",
                table: "CashflowStatements",
                newName: "NCashIncr");

            migrationBuilder.RenameColumn(
                name: "StCashOutAct",
                table: "CashflowStatements",
                newName: "InvestPayCash");

            migrationBuilder.RenameColumn(
                name: "RecpTaxRends",
                table: "CashflowStatements",
                newName: "CashRecSg");

            migrationBuilder.RenameColumn(
                name: "ProvDeprAssets",
                table: "CashflowStatements",
                newName: "CashRecInvest");

            migrationBuilder.RenameColumn(
                name: "ProcIssueBonds",
                table: "CashflowStatements",
                newName: "CashRecCap");

            migrationBuilder.RenameColumn(
                name: "PremFrOrigContr",
                table: "CashflowStatements",
                newName: "CashRecBorrow");

            migrationBuilder.RenameColumn(
                name: "PayHandlingChrg",
                table: "CashflowStatements",
                newName: "CashPayGoods");

            migrationBuilder.RenameColumn(
                name: "PayCommInsurPlcy",
                table: "CashflowStatements",
                newName: "CashPayEmp");

            migrationBuilder.RenameColumn(
                name: "Others",
                table: "CashflowStatements",
                newName: "CashPayDist");

            migrationBuilder.RenameColumn(
                name: "OthRecpRalInvAct",
                table: "CashflowStatements",
                newName: "CashPayDebts");

            migrationBuilder.RenameColumn(
                name: "OthPayRalInvAct",
                table: "CashflowStatements",
                newName: "CashEnd");

            migrationBuilder.RenameColumn(
                name: "OthLossAsset",
                table: "CashflowStatements",
                newName: "CashBegin");

            migrationBuilder.RenameColumn(
                name: "Goodwill",
                table: "BalanceSheets",
                newName: "GoodWill");

            migrationBuilder.RenameColumn(
                name: "UseRightAssets",
                table: "BalanceSheets",
                newName: "TradeFinAssets");

            migrationBuilder.RenameColumn(
                name: "UndistrPorfit",
                table: "BalanceSheets",
                newName: "TotalNcaLiab");

            migrationBuilder.RenameColumn(
                name: "TreasuryShare",
                table: "BalanceSheets",
                newName: "TotalNcaAssets");

            migrationBuilder.RenameColumn(
                name: "TransacSeatFee",
                table: "BalanceSheets",
                newName: "ShortLoan");

            migrationBuilder.RenameColumn(
                name: "TradingFl",
                table: "BalanceSheets",
                newName: "MoneyFund");

            migrationBuilder.RenameColumn(
                name: "TradAsset",
                table: "BalanceSheets",
                newName: "LongLoan");

            migrationBuilder.RenameColumn(
                name: "TotalShare",
                table: "BalanceSheets",
                newName: "Inventory");

            migrationBuilder.RenameColumn(
                name: "TotalNcl",
                table: "BalanceSheets",
                newName: "IntangAssets");

            migrationBuilder.RenameColumn(
                name: "TotalNca",
                table: "BalanceSheets",
                newName: "AccountsPayable");

            migrationBuilder.AlterColumn<string>(
                name: "ReportType",
                table: "IncomeStatements",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CompType",
                table: "IncomeStatements",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "AnnounceDate",
                table: "IncomeStatements",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "FReportDate",
                table: "IncomeStatements",
                type: "date",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReportType",
                table: "CashflowStatements",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CompType",
                table: "CashflowStatements",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "AnnounceDate",
                table: "CashflowStatements",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "FReportDate",
                table: "CashflowStatements",
                type: "date",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReportType",
                table: "BalanceSheets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CompType",
                table: "BalanceSheets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "AnnounceDate",
                table: "BalanceSheets",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "FReportDate",
                table: "BalanceSheets",
                type: "date",
                nullable: true);
        }
    }
}
