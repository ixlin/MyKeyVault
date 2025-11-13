# MyKeyVault Finance CLI - 财报数据导出工具

## 项目简介

这是一个跨平台的命令行工具,用于从 MyKeyVault API 获取股票三大财报数据并导出为 Excel 文件。

## 功能特性

- ✅ 跨平台支持 (Windows, macOS, Linux)
- ✅ 交互式命令行界面
- ✅ 自动股票代码转换 (600036 → 600036.SH)
- ✅ 支持查询资产负债表、利润表、现金流量表
- ✅ 导出为 Excel 文件,包含三个 Sheet
- ✅ 字段中文命名,易于理解
- ✅ 配置文件管理 API 凭据

## 使用前准备

### 1. 服务器端配置

确保 MyKeyVault.Web 服务器已配置以下环境变量:

```bash
export TUSHARE_JWT_SECRET="your-jwt-secret-key"
export TUSHARE_ENCRYPTION_KEY="your-encryption-key"
```

然后重启 MyKeyVault.Web 服务:

```bash
sudo systemctl restart mykeyvault.service
```

### 2. 客户端配置

编辑 `appsettings.json` 文件,配置您的 API 凭据:

```json
{
  "ApiSettings": {
    "BaseUrl": "https://mykeyvault.cn",
    "AppId": "your_app_id",
    "AppSecret": "your_app_secret"
  }
}
```

## 使用方法

### 方式一: 直接运行

```bash
cd src/MyKeyVault.Finance.CLI
dotnet run
```

### 方式二: 编译后运行

```bash
cd src/MyKeyVault.Finance.CLI
dotnet build -c Release
cd bin/Release/net8.0
./MyKeyVault.Finance.CLI
```

### 交互流程

1. 程序启动后会自动认证
2. 输入股票代码 (例如: `600036`)
3. 输入报告年度 (例如: `2024`)
4. 选择报告期:
   - 1: 一季度 (03-31)
   - 2: 半年度 (06-30)
   - 3: 三季度 (09-30)
   - 4: 年度 (12-31)
5. 等待数据获取和 Excel 生成
6. Excel 文件将保存在当前目录

### 输出文件命名规则

文件名格式: `{股票代码}_{年度}_{报告期}_{时间戳}.xlsx`

示例: `600036_2024年半年度_20251112_143025.xlsx`

## Excel 文件内容

生成的 Excel 文件包含三个 Sheet:

### 1. 资产负债表
包含公司资产、负债和权益的详细信息,如:
- 货币资金、应收账款、存货等流动资产
- 固定资产、无形资产、商誉等非流动资产
- 负债和股东权益信息

### 2. 利润表
包含公司收入和利润的详细信息,如:
- 营业收入、营业成本
- 各项费用 (销售费用、管理费用、财务费用)
- 营业利润、净利润等

### 3. 现金流量表
包含公司现金流的详细信息,如:
- 经营活动产生的现金流量
- 投资活动产生的现金流量
- 筹资活动产生的现金流量

所有字段均使用中文命名,方便阅读和分析。

## 发布为独立可执行文件

### Windows (x64)

```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

输出目录: `bin/Release/net8.0/win-x64/publish/`

### macOS (x64)

```bash
dotnet publish -c Release -r osx-x64 --self-contained true /p:PublishSingleFile=true
```

输出目录: `bin/Release/net8.0/osx-x64/publish/`

### macOS (ARM64)

```bash
dotnet publish -c Release -r osx-arm64 --self-contained true /p:PublishSingleFile=true
```

输出目录: `bin/Release/net8.0/osx-arm64/publish/`

### Linux (x64)

```bash
dotnet publish -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true
```

输出目录: `bin/Release/net8.0/linux-x64/publish/`

## 依赖项

- .NET 8.0
- EPPlus 8.2.1 (Excel 生成)
- Microsoft.Extensions.Configuration (配置管理)

## 注意事项

1. **许可证**: EPPlus 用于非商业用途
2. **API 限制**: 请遵守 MyKeyVault API 的调用频率限制
3. **数据准确性**: 财报数据来自 Tushare,仅供参考
4. **配置文件**: 确保 `appsettings.json` 与可执行文件在同一目录

## 常见问题

### Q: 认证失败怎么办?
A: 检查 `appsettings.json` 中的 AppId 和 AppSecret 是否正确,以及服务器是否已配置 JwtSecret。

### Q: 找不到股票数据怎么办?
A: 确认股票代码正确,以及该股票在指定报告期是否有财报数据。

### Q: Excel 文件乱码怎么办?
A: EPPlus 生成的 Excel 文件编码正确,如有乱码请使用 Microsoft Excel 或 WPS 打开。

## 技术支持

如有问题,请联系项目维护者或提交 Issue。
