using System.ComponentModel.DataAnnotations;

namespace MyKeyVault.Web.Models;

/// <summary>
/// 股票列表基础信息（对应 Tushare stock_basic 接口）
/// </summary>
public class StockBasic
{
    [Key]
    [MaxLength(20)]
    public string TsCode { get; set; } = string.Empty; // TS代码（主键）

    [MaxLength(20)]
    public string? Symbol { get; set; } // 股票代码

    [MaxLength(100)]
    public string? Name { get; set; } // 股票名称

    [MaxLength(10)]
    public string? Area { get; set; } // 地域

    [MaxLength(10)]
    public string? Industry { get; set; } // 所属行业

    [MaxLength(100)]
    public string? Fullname { get; set; } // 股票全称

    [MaxLength(10)]
    public string? EnName { get; set; } // 英文全称（简化存储）

    [MaxLength(10)]
    public string? Market { get; set; } // 市场类型（主板/创业板等）

    [MaxLength(10)]
    public string? Exchange { get; set; } // 交易所代码

    [MaxLength(10)]
    public string? CurrType { get; set; } // 交易货币

    public DateOnly? ListDate { get; set; } // 上市日期

    public DateOnly? DelistDate { get; set; } // 退市日期

    [MaxLength(1)]
    public string? IsHs { get; set; } // 是否沪深港通标的（N否 H沪股通 S深股通）

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
