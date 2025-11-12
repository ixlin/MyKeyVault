using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace MyKeyVault.Web.Models;

/// <summary>
/// 股票日线行情（对应 Tushare daily 接口）
/// </summary>
[Index(nameof(TsCode), nameof(TradeDate), IsUnique = true)]
[Index(nameof(TsCode))]
[Index(nameof(TradeDate))]
public class StockDaily
{
    [Key]
    public long Id { get; set; }

    [Required]
    [MaxLength(20)]
    public string TsCode { get; set; } = string.Empty; // 股票代码

    [Required]
    public DateOnly TradeDate { get; set; } // 交易日期

    public decimal? Open { get; set; } // 开盘价
    public decimal? High { get; set; } // 最高价
    public decimal? Low { get; set; } // 最低价
    public decimal? Close { get; set; } // 收盘价
    public decimal? PreClose { get; set; } // 昨收价
    public decimal? Change { get; set; } // 涨跌额
    public decimal? PctChg { get; set; } // 涨跌幅 (%)
    public decimal? Vol { get; set; } // 成交量（手）
    public decimal? Amount { get; set; } // 成交额（千元）

    public DateTime SourceUpdatedAt { get; set; } = DateTime.UtcNow; // 首次拉取时间
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow; // 最近更新时间
}
