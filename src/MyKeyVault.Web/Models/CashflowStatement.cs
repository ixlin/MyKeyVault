using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace MyKeyVault.Web.Models;

/// <summary>
/// 现金流量表（对应 Tushare cashflow 接口）
/// 官方文档：https://tushare.pro/document/2?doc_id=44
/// </summary>
[Index(nameof(TsCode), nameof(EndDate), IsUnique = true)]
[Index(nameof(TsCode))]
[Index(nameof(EndDate))]
public class CashflowStatement
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
    public string? CompType { get; set; } // 公司类型(1一般工商业2银行3保险4证券)

    [MaxLength(10)]
    public string? ReportType { get; set; } // 报表类型

    [MaxLength(10)]
    public string? EndType { get; set; } // 报告期类型

    // ========== 间接法补充项 ==========
    public decimal? NetProfit { get; set; } // 净利润
    public decimal? FinanExp { get; set; } // 财务费用

    // ========== 经营活动现金流 ==========
    public decimal? CFrSaleSg { get; set; } // 销售商品、提供劳务收到的现金
    public decimal? RecpTaxRends { get; set; } // 收到的税费返还
    public decimal? NDeposIncrFi { get; set; } // 客户存款和同业存放款项净增加额
    public decimal? NIncrLoansCb { get; set; } // 向中央银行借款净增加额
    public decimal? NIncBorrOthFi { get; set; } // 向其他金融机构拆入资金净增加额
    public decimal? PremFrOrigContr { get; set; } // 收到原保险合同保费取得的现金
    public decimal? NIncrInsuredDep { get; set; } // 保户储金净增加额
    public decimal? NReinsurPrem { get; set; } // 收到再保业务现金净额
    public decimal? NIncrDispTfa { get; set; } // 处置交易性金融资产净增加额
    public decimal? IfcCashIncr { get; set; } // 收取利息和手续费净增加额
    public decimal? NIncrDispFaas { get; set; } // 处置可供出售金融资产净增加额
    public decimal? NIncrLoansOthBank { get; set; } // 拆入资金净增加额
    public decimal? NCapIncrRepur { get; set; } // 回购业务资金净增加额
    public decimal? CFrOthOperateA { get; set; } // 收到其他与经营活动有关的现金
    public decimal? CInfFrOperateA { get; set; } // 经营活动现金流入小计
    public decimal? CPaidGoodsS { get; set; } // 购买商品、接受劳务支付的现金
    public decimal? CPaidToForEmpl { get; set; } // 支付给职工以及为职工支付的现金
    public decimal? CPaidForTaxes { get; set; } // 支付的各项税费
    public decimal? NIncrCltLoanAdv { get; set; } // 客户贷款及垫款净增加额
    public decimal? NIncrDepCbob { get; set; } // 存放央行和同业款项净增加额
    public decimal? CPayClaimsOrigInco { get; set; } // 支付原保险合同赔付款项的现金
    public decimal? PayHandlingChrg { get; set; } // 支付手续费的现金
    public decimal? PayCommInsurPlcy { get; set; } // 支付保单红利的现金
    public decimal? OthCashPayOperAct { get; set; } // 支付其他与经营活动有关的现金
    public decimal? StCashOutAct { get; set; } // 经营活动现金流出小计
    public decimal? NCashflowAct { get; set; } // 经营活动产生的现金流量净额

    // ========== 投资活动现金流 ==========
    public decimal? OthRecpRalInvAct { get; set; } // 收到其他与投资活动有关的现金
    public decimal? CDispWithdrwlInvest { get; set; } // 收回投资收到的现金
    public decimal? CRecpReturnInvest { get; set; } // 取得投资收益收到的现金
    public decimal? NRecpDispFilta { get; set; } // 处置固定资产、无形资产和其他长期资产收回的现金净额
    public decimal? NRecpDispSobu { get; set; } // 处置子公司及其他营业单位收到的现金净额
    public decimal? StotInflowsInvAct { get; set; } // 投资活动现金流入小计
    public decimal? CPayAcqConstFilta { get; set; } // 购建固定资产、无形资产和其他长期资产支付的现金
    public decimal? CPaidInvest { get; set; } // 投资支付的现金
    public decimal? NDispSubsOthBiz { get; set; } // 取得子公司及其他营业单位支付的现金净额
    public decimal? OthPayRalInvAct { get; set; } // 支付其他与投资活动有关的现金
    public decimal? NIncrPledgeLoan { get; set; } // 质押贷款净增加额
    public decimal? StotOutInvAct { get; set; } // 投资活动现金流出小计
    public decimal? NCashflowInvAct { get; set; } // 投资活动产生的现金流量净额

    // ========== 筹资活动现金流 ==========
    public decimal? CRecpBorrow { get; set; } // 取得借款收到的现金
    public decimal? ProcIssueBonds { get; set; } // 发行债券收到的现金
    public decimal? OthCashRecpRalFncAct { get; set; } // 收到其他与筹资活动有关的现金
    public decimal? StotCashInFncAct { get; set; } // 筹资活动现金流入小计
    public decimal? FreeCashflow { get; set; } // 企业自由现金流量
    public decimal? CPrepayAmtBorr { get; set; } // 偿还债务支付的现金
    public decimal? CPayDistDpcpIntExp { get; set; } // 分配股利、利润或偿付利息支付的现金
    public decimal? InclDvdProfitPaidScMs { get; set; } // 其中:子公司支付给少数股东的股利、利润
    public decimal? OthCashpayRalFncAct { get; set; } // 支付其他与筹资活动有关的现金
    public decimal? StotCashoutFncAct { get; set; } // 筹资活动现金流出小计
    public decimal? NCashFlowsFncAct { get; set; } // 筹资活动产生的现金流量净额

    // ========== 汇率及现金净增加 ==========
    public decimal? EffFxFluCash { get; set; } // 汇率变动对现金的影响
    public decimal? NIncrCashCashEqu { get; set; } // 现金及现金等价物净增加额
    public decimal? CCashEquBegPeriod { get; set; } // 期初现金及现金等价物余额
    public decimal? CCashEquEndPeriod { get; set; } // 期末现金及现金等价物余额

    // ========== 补充资料 ==========
    public decimal? CRecpCapContrib { get; set; } // 吸收投资收到的现金
    public decimal? InclCashRecSaims { get; set; } // 其中:子公司吸收少数股东投资收到的现金
    public decimal? UnconInvestLoss { get; set; } // 未确认投资损失
    public decimal? ProvDeprAssets { get; set; } // 加:资产减值准备
    public decimal? DeprFaCogaDpba { get; set; } // 固定资产折旧、油气资产折耗、生产性生物资产折旧
    public decimal? AmortIntangAssets { get; set; } // 无形资产摊销
    public decimal? LtAmortDeferredExp { get; set; } // 长期待摊费用摊销
    public decimal? DecrDeferredExp { get; set; } // 待摊费用减少
    public decimal? IncrAccExp { get; set; } // 预提费用增加
    public decimal? LossDispFilta { get; set; } // 处置固定、无形资产和其他长期资产的损失
    public decimal? LossScrFa { get; set; } // 固定资产报废损失
    public decimal? LossFvChg { get; set; } // 公允价值变动损失
    public decimal? InvestLoss { get; set; } // 投资损失
    public decimal? DecrDefIncTaxAssets { get; set; } // 递延所得税资产减少
    public decimal? IncrDefIncTaxLiab { get; set; } // 递延所得税负债增加
    public decimal? DecrInventories { get; set; } // 存货的减少
    public decimal? DecrOperPayable { get; set; } // 经营性应收项目的减少
    public decimal? IncrOperPayable { get; set; } // 经营性应付项目的增加
    public decimal? Others { get; set; } // 其他
    public decimal? ImNetCashflowOperAct { get; set; } // 经营活动产生的现金流量净额(间接法)
    public decimal? ConvDebtIntoCap { get; set; } // 债务转为资本
    public decimal? ConvCopbondsDueWithin1y { get; set; } // 一年内到期的可转换公司债券
    public decimal? FaFncLeases { get; set; } // 融资租入固定资产
    public decimal? ImNIncrCashEqu { get; set; } // 现金及现金等价物净增加额(间接法)
    public decimal? NetDismCapitalAdd { get; set; } // 拆出资金净增加额
    public decimal? NetCashReceSec { get; set; } // 代理买卖证券收到的现金净额(元)
    public decimal? CreditImpaLoss { get; set; } // 信用减值损失
    public decimal? UseRightAssetDep { get; set; } // 使用权资产折旧
    public decimal? OthLossAsset { get; set; } // 其他资产减值损失
    public decimal? EndBalCash { get; set; } // 现金的期末余额
    public decimal? BegBalCash { get; set; } // 减:现金的期初余额
    public decimal? EndBalCashEqu { get; set; } // 加:现金等价物的期末余额
    public decimal? BegBalCashEqu { get; set; } // 减:现金等价物的期初余额

    [MaxLength(10)]
    public string? UpdateFlag { get; set; } // 更新标志(1最新）

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
