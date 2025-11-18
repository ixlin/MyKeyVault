using Microsoft.Extensions.Configuration;
using MyKeyVault.Finance.CLI.Models;
using MyKeyVault.Finance.CLI.Services;
using System.Text.Json;

// 读取配置文件
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var apiSettings = new ApiSettings();
configuration.GetSection("ApiSettings").Bind(apiSettings);

// 初始化服务
var apiService = new TushareApiService(apiSettings);
var excelService = new ExcelExportService();

Console.WriteLine("===========================================");
Console.WriteLine("      MyKeyVault 财报数据导出工具");
Console.WriteLine("===========================================");
Console.WriteLine();

// 认证
Console.WriteLine("正在认证...");
if (!await apiService.AuthenticateAsync())
{
    Console.WriteLine("认证失败,请检查 appsettings.json 中的 AppId 和 AppSecret");
    return;
}
Console.WriteLine("认证成功!");
Console.WriteLine();

// 主循环：持续导出，直到用户强制退出 (Ctrl+C)
while (true)
{
    // 用户输入股票代码（带验证和重试）
    string stockCode;
    string fullStockCode;
    while (true)
    {
        Console.Write("请输入股票代码 (例如: 600036，输入 q 退出): ");
        var input = Console.ReadLine()?.Trim();
        
        if (string.IsNullOrEmpty(input))
        {
            Console.WriteLine("❌ 股票代码不能为空，请重新输入。");
            Console.WriteLine();
            continue;
        }
        
        if (input.Equals("q", StringComparison.OrdinalIgnoreCase) || 
            input.Equals("quit", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("感谢使用，再见！");
            return;
        }
        
        // 简单验证：股票代码应为6位数字
        if (!System.Text.RegularExpressions.Regex.IsMatch(input, @"^\d{6}$"))
        {
            Console.WriteLine("❌ 无效的股票代码格式，股票代码应为6位数字（如 600036）。");
            Console.WriteLine();
            continue;
        }
        
        stockCode = input;
        fullStockCode = apiService.ConvertStockCode(stockCode);
        
        // 验证股票是否存在：尝试获取最近一年的数据
        Console.WriteLine($"正在验证股票代码 {fullStockCode} ...");
        var testParams = new Dictionary<string, object>
        {
            { "ts_code", fullStockCode },
            { "period", $"{DateTime.Now.Year - 1}1231" } // 查询上一年年报
        };
        
        var testResponse = await apiService.QueryAsync("balancesheet", testParams);
        if (testResponse == null || testResponse.Code != 0)
        {
            Console.WriteLine($"❌ 无法查询到股票 {fullStockCode} 的数据，请确认股票代码是否正确。");
            Console.WriteLine("提示：请输入沪深A股的6位代码，如 600036（招商银行）、000001（平安银行）。");
            Console.WriteLine();
            continue;
        }
        
        // 检查是否有数据
        if (testResponse.Data == null || 
            testResponse.Data.Value.ValueKind != JsonValueKind.Array ||
            testResponse.Data.Value.GetArrayLength() == 0)
        {
            Console.WriteLine($"❌ 股票代码 {fullStockCode} 未查询到任何财务数据，请确认是否为有效的上市公司代码。");
            Console.WriteLine();
            continue;
        }
        
        Console.WriteLine($"✓ 股票代码验证通过: {fullStockCode}");
        Console.WriteLine();
        break;
    }

    // 选择报告年度（带验证和重试）
    int year;
    while (true)
    {
        Console.Write($"请输入报告年度 (1990-{DateTime.Now.Year}): ");
        var yearInput = Console.ReadLine()?.Trim();
        
        if (string.IsNullOrEmpty(yearInput))
        {
            Console.WriteLine("❌ 年度不能为空，请重新输入。");
            Console.WriteLine();
            continue;
        }
        
        if (!int.TryParse(yearInput, out year))
        {
            Console.WriteLine("❌ 无效的年度格式，请输入数字（如 2024）。");
            Console.WriteLine();
            continue;
        }
        
        if (year < 1990 || year > DateTime.Now.Year)
        {
            Console.WriteLine($"❌ 年度超出范围，请输入 1990 到 {DateTime.Now.Year} 之间的年份。");
            Console.WriteLine();
            continue;
        }
        
        break;
    }

    // 选择报告期（带验证和重试）
    string endDate;
    string periodName;
    while (true)
    {
        Console.WriteLine();
        Console.WriteLine("请选择报告期:");
        Console.WriteLine("  1. 一季度 (03-31)");
        Console.WriteLine("  2. 半年度 (06-30)");
        Console.WriteLine("  3. 三季度 (09-30)");
        Console.WriteLine("  4. 年度 (12-31)");
        Console.Write("请输入选项 (1-4): ");
        var periodChoice = Console.ReadLine()?.Trim();
        
        if (string.IsNullOrEmpty(periodChoice))
        {
            Console.WriteLine("❌ 报告期不能为空，请重新选择。");
            continue;
        }
        
        switch (periodChoice)
        {
            case "1":
                endDate = $"{year}0331";
                periodName = "一季度";
                break;
            case "2":
                endDate = $"{year}0630";
                periodName = "半年度";
                break;
            case "3":
                endDate = $"{year}0930";
                periodName = "三季度";
                break;
            case "4":
                endDate = $"{year}1231";
                periodName = "年度";
                break;
            default:
                Console.WriteLine("❌ 无效的选项，请输入 1-4 之间的数字。");
                continue;
        }
        
        break;
    }

Console.WriteLine();
Console.WriteLine($"正在获取 {fullStockCode} {year}年{periodName}报告的三大财报数据...");
Console.WriteLine();

// 查询参数
var queryParams = new Dictionary<string, object>
{
    { "ts_code", fullStockCode },
    { "period", endDate }
};

    // 查询资产负债表
    Console.WriteLine("1/3 正在获取资产负债表...");
    var balanceSheetResponse = await apiService.QueryAsync("balancesheet", queryParams);
    if (balanceSheetResponse == null || balanceSheetResponse.Code != 0)
    {
        Console.WriteLine($"❌ 获取资产负债表失败: {balanceSheetResponse?.Message ?? "未知错误"}");
        Console.WriteLine("请检查股票代码和报告期是否正确，然后重新尝试。");
        Console.WriteLine();
        continue; // 回到主菜单
    }
    
    // 检查资产负债表数据是否为空
    if (balanceSheetResponse.Data == null || 
        balanceSheetResponse.Data.Value.ValueKind != JsonValueKind.Array ||
        balanceSheetResponse.Data.Value.GetArrayLength() == 0)
    {
        Console.WriteLine($"❌ 未查询到 {fullStockCode} 在 {year}年{periodName} 的资产负债表数据。");
        Console.WriteLine("可能原因：");
        Console.WriteLine("  1. 该报告期的财报尚未公布");
        Console.WriteLine("  2. 公司在该期间未上市或已退市");
        Console.WriteLine("请重新选择其他年度或报告期。");
        Console.WriteLine();
        continue;
    }

    // 查询利润表
    Console.WriteLine("2/3 正在获取利润表...");
    var incomeResponse = await apiService.QueryAsync("income", queryParams);
    if (incomeResponse == null || incomeResponse.Code != 0)
    {
        Console.WriteLine($"❌ 获取利润表失败: {incomeResponse?.Message ?? "未知错误"}");
        Console.WriteLine("请检查股票代码和报告期是否正确，然后重新尝试。");
        Console.WriteLine();
        continue; // 回到主菜单
    }
    
    // 检查利润表数据是否为空
    if (incomeResponse.Data == null || 
        incomeResponse.Data.Value.ValueKind != JsonValueKind.Array ||
        incomeResponse.Data.Value.GetArrayLength() == 0)
    {
        Console.WriteLine($"❌ 未查询到 {fullStockCode} 在 {year}年{periodName} 的利润表数据。");
        Console.WriteLine("请重新选择其他年度或报告期。");
        Console.WriteLine();
        continue;
    }

    // 查询现金流量表
    Console.WriteLine("3/3 正在获取现金流量表...");
    var cashflowResponse = await apiService.QueryAsync("cashflow", queryParams);
    if (cashflowResponse == null || cashflowResponse.Code != 0)
    {
        Console.WriteLine($"❌ 获取现金流量表失败: {cashflowResponse?.Message ?? "未知错误"}");
        Console.WriteLine("请检查股票代码和报告期是否正确，然后重新尝试。");
        Console.WriteLine();
        continue; // 回到主菜单
    }
    
    // 检查现金流量表数据是否为空
    if (cashflowResponse.Data == null || 
        cashflowResponse.Data.Value.ValueKind != JsonValueKind.Array ||
        cashflowResponse.Data.Value.GetArrayLength() == 0)
    {
        Console.WriteLine($"❌ 未查询到 {fullStockCode} 在 {year}年{periodName} 的现金流量表数据。");
        Console.WriteLine("请重新选择其他年度或报告期。");
        Console.WriteLine();
        continue;
    }

    Console.WriteLine();
    Console.WriteLine("✓ 数据获取完成，正在生成 Excel 文件...");

    // 生成文件名: 股票代码_年度_报告期_时间戳.xlsx
    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
    var fileName = $"{stockCode}_{year}年{periodName}_{timestamp}.xlsx";
    var filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);

    try
    {
        // 提取数据部分
        JsonElement? balanceSheetData = balanceSheetResponse.Data;
        JsonElement? incomeData = incomeResponse.Data;
        JsonElement? cashflowData = cashflowResponse.Data;

        excelService.ExportFinancialStatements(filePath, balanceSheetData, incomeData, cashflowData);
        
        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine("✓ 导出成功!");
        Console.WriteLine($"文件位置: {filePath}");
        Console.WriteLine("========================================");
        Console.WriteLine();
        Console.WriteLine("继续导出下一个股票，或按 Ctrl+C 退出程序。");
        Console.WriteLine();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ 导出失败: {ex.Message}");
        Console.WriteLine("请重新尝试或联系技术支持。");
        Console.WriteLine();
    }
} // while (true) 结束
