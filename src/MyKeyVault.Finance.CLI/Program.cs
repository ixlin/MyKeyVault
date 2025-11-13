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

// 用户输入股票代码
Console.Write("请输入股票代码 (例如: 600036): ");
var stockCode = Console.ReadLine()?.Trim();
if (string.IsNullOrEmpty(stockCode))
{
    Console.WriteLine("股票代码不能为空");
    return;
}

// 转换为完整格式
var fullStockCode = apiService.ConvertStockCode(stockCode);
Console.WriteLine($"转换后的股票代码: {fullStockCode}");
Console.WriteLine();

// 选择报告年度
Console.Write("请输入报告年度 (例如: 2024): ");
var yearInput = Console.ReadLine()?.Trim();
if (!int.TryParse(yearInput, out var year) || year < 1990 || year > DateTime.Now.Year)
{
    Console.WriteLine("无效的年度");
    return;
}

// 选择报告期
Console.WriteLine();
Console.WriteLine("请选择报告期:");
Console.WriteLine("1. 一季度 (03-31)");
Console.WriteLine("2. 半年度 (06-30)");
Console.WriteLine("3. 三季度 (09-30)");
Console.WriteLine("4. 年度 (12-31)");
Console.Write("请输入选项 (1-4): ");
var periodChoice = Console.ReadLine()?.Trim();

string endDate;
string periodName;
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
        Console.WriteLine("无效的选项");
        return;
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
    Console.WriteLine($"获取资产负债表失败: {balanceSheetResponse?.Message ?? "未知错误"}");
    return;
}

// 查询利润表
Console.WriteLine("2/3 正在获取利润表...");
var incomeResponse = await apiService.QueryAsync("income", queryParams);
if (incomeResponse == null || incomeResponse.Code != 0)
{
    Console.WriteLine($"获取利润表失败: {incomeResponse?.Message ?? "未知错误"}");
    return;
}

// 查询现金流量表
Console.WriteLine("3/3 正在获取现金流量表...");
var cashflowResponse = await apiService.QueryAsync("cashflow", queryParams);
if (cashflowResponse == null || cashflowResponse.Code != 0)
{
    Console.WriteLine($"获取现金流量表失败: {cashflowResponse?.Message ?? "未知错误"}");
    return;
}

Console.WriteLine();
Console.WriteLine("数据获取完成,正在生成 Excel 文件...");

// 生成文件名: 股票代码_年度_报告期_时间戳.xlsx
var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
var fileName = $"{stockCode}_{year}年{periodName}_{timestamp}.xlsx";
var filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);

try
{
    // 提取数据部分
    JsonElement? balanceSheetData = balanceSheetResponse.Data != null 
        ? JsonSerializer.SerializeToElement(balanceSheetResponse.Data) 
        : null;
    
    JsonElement? incomeData = incomeResponse.Data != null 
        ? JsonSerializer.SerializeToElement(incomeResponse.Data) 
        : null;
    
    JsonElement? cashflowData = cashflowResponse.Data != null 
        ? JsonSerializer.SerializeToElement(cashflowResponse.Data) 
        : null;

    excelService.ExportFinancialStatements(filePath, balanceSheetData, incomeData, cashflowData);
    
    Console.WriteLine();
    Console.WriteLine("========================================");
    Console.WriteLine($"✓ 导出成功!");
    Console.WriteLine($"文件位置: {filePath}");
    Console.WriteLine("========================================");
}
catch (Exception ex)
{
    Console.WriteLine($"导出失败: {ex.Message}");
    Console.WriteLine($"详细信息: {ex.StackTrace}");
}
