using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace MyKeyVault.Web.Models;

/// <summary>
/// 利润表（对应 Tushare income 接口）
/// 官方文档：https://tushare.pro/document/2?doc_id=33
/// </summary>
[Index(nameof(TsCode), nameof(EndDate), IsUnique = true)]
[Index(nameof(TsCode))]
[Index(nameof(EndDate))]
public class IncomeStatement
{
    [Key]
    public long Id { get; set; }

    // ========== 基础字段 ==========
    [Required]
    [MaxLength(20)]
    public string TsCode { get; set; } = string.Empty; // TS代码
    
    [MaxLength(10)]
    public string? AnnDate { get; set; } // 公告日期
    
    [MaxLength(10)]
    public string? FAnnDate { get; set; } // 实际公告日期

    [Required]
    public DateOnly EndDate { get; set; } // 报告期

    [MaxLength(10)]
    public string? ReportType { get; set; } // 报告类型 见底部表

    [MaxLength(10)]
    public string? CompType { get; set; } // 公司类型(1一般工商业2银行3保险4证券)
    
    [MaxLength(10)]
    public string? EndType { get; set; } // 报告期类型

    // ========== 每股指标 ==========
    public decimal? BasicEps { get; set; } // 基本每股收益
    public decimal? DilutedEps { get; set; } // 稀释每股收益

    // ========== 收入项目 ==========
    public decimal? TotalRevenue { get; set; } // 营业总收入
    public decimal? Revenue { get; set; } // 营业收入
    public decimal? IntIncome { get; set; } // 利息收入
    public decimal? PremEarned { get; set; } // 已赚保费
    public decimal? CommIncome { get; set; } // 手续费及佣金收入
    public decimal? NCommisIncome { get; set; } // 手续费及佣金净收入
    public decimal? NOthIncome { get; set; } // 其他经营净收益
    public decimal? NOthBIncome { get; set; } // 加:其他业务净收益
    public decimal? PremIncome { get; set; } // 保险业务收入
    public decimal? OutPrem { get; set; } // 减:分出保费
    public decimal? UnePremReser { get; set; } // 提取未到期责任准备金
    public decimal? ReinsIncome { get; set; } // 其中:分保费收入
    public decimal? NSecTbIncome { get; set; } // 代理买卖证券业务净收入
    public decimal? NSecUwIncome { get; set; } // 证券承销业务净收入
    public decimal? NAssetMgIncome { get; set; } // 受托客户资产管理业务净收入
    public decimal? OthBIncome { get; set; } // 其他业务收入
    public decimal? FvValueChgGain { get; set; } // 加:公允价值变动净收益
    public decimal? InvestIncome { get; set; } // 加:投资净收益
    public decimal? AssInvestIncome { get; set; } // 其中:对联营企业和合营企业的投资收益
    public decimal? ForexGain { get; set; } // 加:汇兑净收益

    // ========== 成本费用 ==========
    public decimal? TotalCogs { get; set; } // 营业总成本
    public decimal? OperCost { get; set; } // 减:营业成本
    public decimal? IntExp { get; set; } // 减:利息支出
    public decimal? CommExp { get; set; } // 减:手续费及佣金支出
    public decimal? BizTaxSurchg { get; set; } // 减:营业税金及附加
    public decimal? SellExp { get; set; } // 减:销售费用
    public decimal? AdminExp { get; set; } // 减:管理费用
    public decimal? FinExp { get; set; } // 减:财务费用
    public decimal? AssetsImpairLoss { get; set; } // 减:资产减值损失
    public decimal? PremRefund { get; set; } // 退保金
    public decimal? CompensPayout { get; set; } // 赔付总支出
    public decimal? ReserInsurLiab { get; set; } // 提取保险责任准备金
    public decimal? DivPayt { get; set; } // 保户红利支出
    public decimal? ReinsExp { get; set; } // 分保费用
    public decimal? OperExp { get; set; } // 营业支出
    public decimal? CompensPayoutRefu { get; set; } // 减:摊回赔付支出
    public decimal? InsurReserRefu { get; set; } // 减:摊回保险责任准备金
    public decimal? ReinsCostRefund { get; set; } // 减:摊回分保费用
    public decimal? OtherBusCost { get; set; } // 其他业务成本

    // ========== 利润项目 ==========
    public decimal? OperateProfit { get; set; } // 营业利润
    public decimal? NonOperIncome { get; set; } // 加:营业外收入
    public decimal? NonOperExp { get; set; } // 减:营业外支出
    public decimal? NcaDisploss { get; set; } // 其中:减:非流动资产处置净损失
    public decimal? TotalProfit { get; set; } // 利润总额
    public decimal? IncomeTax { get; set; } // 所得税费用
    public decimal? NIncome { get; set; } // 净利润(含少数股东损益)
    public decimal? NIncomeAttrP { get; set; } // 净利润(不含少数股东损益)
    public decimal? MinorityGain { get; set; } // 少数股东损益
    public decimal? OthComprIncome { get; set; } // 其他综合收益
    public decimal? TComprIncome { get; set; } // 综合收益总额
    public decimal? ComprIncAttrP { get; set; } // 归属于母公司(或股东)的综合收益总额
    public decimal? ComprIncAttrMS { get; set; } // 归属于少数股东的综合收益总额

    // ========== 其他指标 ==========
    public decimal? Ebit { get; set; } // 息税前利润
    public decimal? Ebitda { get; set; } // 息税折旧摊销前利润
    public decimal? InsuranceExp { get; set; } // 保险业务支出
    public decimal? UndistProfit { get; set; } // 年初未分配利润
    public decimal? DistableProfit { get; set; } // 可分配利润
    public decimal? RdExp { get; set; } // 研发费用
    public decimal? FinExpIntExp { get; set; } // 财务费用:利息费用
    public decimal? FinExpIntInc { get; set; } // 财务费用:利息收入

    // ========== 利润分配 ==========
    public decimal? TransferSurplusRese { get; set; } // 盈余公积转入
    public decimal? TransferHousingImprest { get; set; } // 住房周转金转入
    public decimal? TransferOth { get; set; } // 其他转入
    public decimal? AdjLossgain { get; set; } // 调整以前年度损益
    public decimal? WithdraLegalSurplus { get; set; } // 提取法定盈余公积
    public decimal? WithdraLegalPubfund { get; set; } // 提取法定公益金
    public decimal? WithdraBizDevfund { get; set; } // 提取企业发展基金
    public decimal? WithdraReseFund { get; set; } // 提取储备基金
    public decimal? WithdraOthErsu { get; set; } // 提取任意盈余公积金
    public decimal? WorkersWelfare { get; set; } // 职工奖金福利
    public decimal? DistrProfitShrhder { get; set; } // 可供股东分配的利润
    public decimal? PrfsharePayableDvd { get; set; } // 应付优先股股利
    public decimal? ComsharePayableDvd { get; set; } // 应付普通股股利
    public decimal? CapitComstockDiv { get; set; } // 转作股本的普通股股利

    // ========== 扩展字段（N = 可选） ==========
    public decimal? NetAfterNrLpCorrect { get; set; } // 扣除非经常性损益后的净利润（更正前）
    public decimal? CreditImpaLoss { get; set; } // 信用减值损失
    public decimal? NetExpoHedgingBenefits { get; set; } // 净敞口套期收益
    public decimal? OthImpairLossAssets { get; set; } // 其他资产减值损失
    public decimal? TotalOpcost { get; set; } // 营业总成本（二）
    public decimal? AmodcostFinAssets { get; set; } // 以摊余成本计量的金融资产终止确认收益
    public decimal? OthIncome { get; set; } // 其他收益
    public decimal? AssetDispIncome { get; set; } // 资产处置收益
    public decimal? ContinuedNetProfit { get; set; } // 持续经营净利润
    public decimal? EndNetProfit { get; set; } // 终止经营净利润

    [MaxLength(10)]
    public string? UpdateFlag { get; set; } // 更新标识

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
