using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace MyKeyVault.Web.Models;

/// <summary>
/// 资产负债表（对应 Tushare balancesheet 接口）
/// 官方文档：https://tushare.pro/document/2?doc_id=36
/// </summary>
[Index(nameof(TsCode), nameof(EndDate), IsUnique = true)]
[Index(nameof(TsCode))]
[Index(nameof(EndDate))]
public class BalanceSheet
{
    [Key]
    public long Id { get; set; }

    // ========== 基础字段 ==========
    [Required]
    [MaxLength(20)]
    public string TsCode { get; set; } = string.Empty; // TS股票代码

    [MaxLength(10)]
    public string? AnnDate { get; set; } // 公告日期

    [MaxLength(10)]
    public string? FAnnDate { get; set; } // 实际公告日期

    [Required]
    public DateOnly EndDate { get; set; } // 报告期

    [MaxLength(10)]
    public string? ReportType { get; set; } // 报表类型

    [MaxLength(10)]
    public string? CompType { get; set; } // 公司类型(1一般工商业2银行3保险4证券)

    [MaxLength(10)]
    public string? EndType { get; set; } // 报告期类型

    // ========== 股本及储备 ==========
    public decimal? TotalShare { get; set; } // 期末总股本
    public decimal? CapRese { get; set; } // 资本公积金
    public decimal? UndistrPorfit { get; set; } // 未分配利润
    public decimal? SurplusRese { get; set; } // 盈余公积金
    public decimal? SpecialRese { get; set; } // 专项储备

    // ========== 流动资产 ==========
    public decimal? MoneyCap { get; set; } // 货币资金
    public decimal? TradAsset { get; set; } // 交易性金融资产
    public decimal? NotesReceiv { get; set; } // 应收票据
    public decimal? AccountsReceiv { get; set; } // 应收账款
    public decimal? OthReceiv { get; set; } // 其他应收款
    public decimal? Prepayment { get; set; } // 预付款项
    public decimal? DivReceiv { get; set; } // 应收股利
    public decimal? IntReceiv { get; set; } // 应收利息
    public decimal? Inventories { get; set; } // 存货
    public decimal? AmorExp { get; set; } // 待摊费用
    public decimal? NcaWithin1y { get; set; } // 一年内到期的非流动资产
    public decimal? SettRsrv { get; set; } // 结算备付金
    public decimal? LoantoOthBankFi { get; set; } // 拆出资金
    public decimal? PremiumReceiv { get; set; } // 应收保费
    public decimal? ReinsurReceiv { get; set; } // 应收分保账款
    public decimal? ReinsurResReceiv { get; set; } // 应收分保合同准备金
    public decimal? PurResaleFa { get; set; } // 买入返售金融资产
    public decimal? OthCurAssets { get; set; } // 其他流动资产
    public decimal? TotalCurAssets { get; set; } // 流动资产合计

    // ========== 非流动资产 ==========
    public decimal? FaAvailForSale { get; set; } // 可供出售金融资产
    public decimal? HtmInvest { get; set; } // 持有至到期投资
    public decimal? LtEqtInvest { get; set; } // 长期股权投资
    public decimal? InvestRealEstate { get; set; } // 投资性房地产
    public decimal? TimeDeposits { get; set; } // 定期存款
    public decimal? OthAssets { get; set; } // 其他资产
    public decimal? LtRec { get; set; } // 长期应收款
    public decimal? FixAssets { get; set; } // 固定资产
    public decimal? Cip { get; set; } // 在建工程
    public decimal? ConstMaterials { get; set; } // 工程物资
    public decimal? FixedAssetsDisp { get; set; } // 固定资产清理
    public decimal? ProducBioAssets { get; set; } // 生产性生物资产
    public decimal? OilAndGasAssets { get; set; } // 油气资产
    public decimal? IntanAssets { get; set; } // 无形资产
    public decimal? RAndD { get; set; } // 研发支出
    public decimal? Goodwill { get; set; } // 商誉
    public decimal? LtAmorExp { get; set; } // 长期待摊费用
    public decimal? DeferTaxAssets { get; set; } // 递延所得税资产
    public decimal? DecrInDisbur { get; set; } // 发放贷款及垫款
    public decimal? OthNca { get; set; } // 其他非流动资产
    public decimal? TotalNca { get; set; } // 非流动资产合计

    // ========== 银行/金融/保险特有资产 ==========
    public decimal? CashReserCb { get; set; } // 现金及存放中央银行款项
    public decimal? DeposInOthBfi { get; set; } // 存放同业和其它金融机构款项
    public decimal? PrecMetals { get; set; } // 贵金属
    public decimal? DerivAssets { get; set; } // 衍生金融资产
    public decimal? RrReinsUnePrem { get; set; } // 应收分保未到期责任准备金
    public decimal? RrReinsOutstdCla { get; set; } // 应收分保未决赔款准备金
    public decimal? RrReinsLinsLiab { get; set; } // 应收分保寿险责任准备金
    public decimal? RrReinsLthinsLiab { get; set; } // 应收分保长期健康险责任准备金
    public decimal? RefundDepos { get; set; } // 存出保证金
    public decimal? PhPledgeLoans { get; set; } // 保户质押贷款
    public decimal? RefundCapDepos { get; set; } // 存出资本保证金
    public decimal? IndepAcctAssets { get; set; } // 独立账户资产
    public decimal? ClientDepos { get; set; } // 其中：客户资金存款
    public decimal? ClientProv { get; set; } // 其中：客户备付金
    public decimal? TransacSeatFee { get; set; } // 其中:交易席位费
    public decimal? InvestAsReceiv { get; set; } // 应收款项类投资

    // ========== 资产总计 ==========
    public decimal? TotalAssets { get; set; } // 资产总计

    // ========== 流动负债 ==========
    public decimal? LtBorr { get; set; } // 长期借款
    public decimal? StBorr { get; set; } // 短期借款
    public decimal? CbBorr { get; set; } // 向中央银行借款
    public decimal? DeposIbDeposits { get; set; } // 吸收存款及同业存放
    public decimal? LoanOthBank { get; set; } // 拆入资金
    public decimal? TradingFl { get; set; } // 交易性金融负债
    public decimal? NotesPayable { get; set; } // 应付票据
    public decimal? AcctPayable { get; set; } // 应付账款
    public decimal? AdvReceipts { get; set; } // 预收款项
    public decimal? SoldForRepurFa { get; set; } // 卖出回购金融资产款
    public decimal? CommPayable { get; set; } // 应付手续费及佣金
    public decimal? PayrollPayable { get; set; } // 应付职工薪酬
    public decimal? TaxesPayable { get; set; } // 应交税费
    public decimal? IntPayable { get; set; } // 应付利息
    public decimal? DivPayable { get; set; } // 应付股利
    public decimal? OthPayable { get; set; } // 其他应付款
    public decimal? AccExp { get; set; } // 预提费用
    public decimal? DeferredInc { get; set; } // 递延收益
    public decimal? StBondsPayable { get; set; } // 应付短期债券
    public decimal? PayableToReinsurer { get; set; } // 应付分保账款
    public decimal? RsrvInsurCont { get; set; } // 保险合同准备金
    public decimal? ActingTradingSec { get; set; } // 代理买卖证券款
    public decimal? ActingUwSec { get; set; } // 代理承销证券款
    public decimal? NonCurLiabDue1y { get; set; } // 一年内到期的非流动负债
    public decimal? OthCurLiab { get; set; } // 其他流动负债
    public decimal? TotalCurLiab { get; set; } // 流动负债合计

    // ========== 非流动负债 ==========
    public decimal? BondPayable { get; set; } // 应付债券
    public decimal? LtPayable { get; set; } // 长期应付款
    public decimal? SpecificPayables { get; set; } // 专项应付款
    public decimal? EstimatedLiab { get; set; } // 预计负债
    public decimal? DeferTaxLiab { get; set; } // 递延所得税负债
    public decimal? DeferIncNonCurLiab { get; set; } // 递延收益-非流动负债
    public decimal? OthNcl { get; set; } // 其他非流动负债
    public decimal? TotalNcl { get; set; } // 非流动负债合计

    // ========== 银行/金融/保险特有负债 ==========
    public decimal? DeposOthBfi { get; set; } // 同业和其它金融机构存放款项
    public decimal? DerivLiab { get; set; } // 衍生金融负债
    public decimal? Depos { get; set; } // 吸收存款
    public decimal? AgencyBusLiab { get; set; } // 代理业务负债
    public decimal? OthLiab { get; set; } // 其他负债
    public decimal? PremReceivAdva { get; set; } // 预收保费
    public decimal? DeposReceived { get; set; } // 存入保证金
    public decimal? PhInvest { get; set; } // 保户储金及投资款
    public decimal? ReserUnePrem { get; set; } // 未到期责任准备金
    public decimal? ReserOutstdClaims { get; set; } // 未决赔款准备金
    public decimal? ReserLinsLiab { get; set; } // 寿险责任准备金
    public decimal? ReserLthinsLiab { get; set; } // 长期健康险责任准备金
    public decimal? IndeptAccLiab { get; set; } // 独立账户负债
    public decimal? PledgeBorr { get; set; } // 其中:质押借款
    public decimal? IndemPayable { get; set; } // 应付赔付款
    public decimal? PolicyDivPayable { get; set; } // 应付保单红利

    // ========== 负债合计 ==========
    public decimal? TotalLiab { get; set; } // 负债合计

    // ========== 所有者权益 ==========
    public decimal? TreasuryShare { get; set; } // 减:库存股
    public decimal? OrdinRiskReser { get; set; } // 一般风险准备
    public decimal? ForexDiffer { get; set; } // 外币报表折算差额
    public decimal? InvestLossUnconf { get; set; } // 未确认的投资损失
    public decimal? MinorityInt { get; set; } // 少数股东权益
    public decimal? TotalHldrEqyExcMinInt { get; set; } // 股东权益合计(不含少数股东权益)
    public decimal? TotalHldrEqyIncMinInt { get; set; } // 股东权益合计(含少数股东权益)

    // ========== 负债及股东权益总计 ==========
    public decimal? TotalLiabHldrEqy { get; set; } // 负债及股东权益总计

    // ========== 补充字段 ==========
    public decimal? LtPayrollPayable { get; set; } // 长期应付职工薪酬
    public decimal? OthCompIncome { get; set; } // 其他综合收益
    public decimal? OthEqtTools { get; set; } // 其他权益工具
    public decimal? OthEqtToolsPShr { get; set; } // 其他权益工具(优先股)
    public decimal? LendingFunds { get; set; } // 融出资金
    public decimal? AccReceivable { get; set; } // 应收款项
    public decimal? StFinPayable { get; set; } // 应付短期融资款
    public decimal? Payables { get; set; } // 应付款项
    public decimal? HfsAssets { get; set; } // 持有待售的资产
    public decimal? HfsSales { get; set; } // 持有待售的负债
    public decimal? CostFinAssets { get; set; } // 以摊余成本计量的金融资产
    public decimal? FairValueFinAssets { get; set; } // 以公允价值计量且其变动计入其他综合收益的金融资产
    public decimal? CipTotal { get; set; } // 在建工程(合计)(元)
    public decimal? OthPayTotal { get; set; } // 其他应付款(合计)(元)
    public decimal? LongPayTotal { get; set; } // 长期应付款(合计)(元)
    public decimal? DebtInvest { get; set; } // 债权投资(元)
    public decimal? OthDebtInvest { get; set; } // 其他债权投资(元)
    public decimal? OthEqInvest { get; set; } // 其他权益工具投资(元)
    public decimal? OthIlliqFinAssets { get; set; } // 其他非流动金融资产(元)
    public decimal? OthEqPpbond { get; set; } // 其他权益工具:永续债(元)
    public decimal? ReceivFinancing { get; set; } // 应收款项融资
    public decimal? UseRightAssets { get; set; } // 使用权资产
    public decimal? LeaseLiab { get; set; } // 租赁负债
    public decimal? ContractAssets { get; set; } // 合同资产
    public decimal? ContractLiab { get; set; } // 合同负债
    public decimal? AccountsReceivBill { get; set; } // 应收票据及应收账款
    public decimal? AccountsPay { get; set; } // 应付票据及应付账款
    public decimal? OthRcvTotal { get; set; } // 其他应收款(合计)（元）
    public decimal? FixAssetsTotal { get; set; } // 固定资产(合计)(元)

    [MaxLength(10)]
    public string? UpdateFlag { get; set; } // 更新标识

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
