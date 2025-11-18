# MyKeyVault Finance CLI - 发布说明

## 📦 发布内容

已成功生成三个平台的自包含可执行程序包：

### Windows x64
- **文件名**: `MyKeyVault.Finance.CLI-win-x64.zip` (31 MB)
- **适用系统**: Windows 10/11 (64位)
- **主程序**: `MyKeyVault.Finance.CLI.exe`

### macOS Intel
- **文件名**: `MyKeyVault.Finance.CLI-osx-x64.zip` (31 MB)
- **适用系统**: macOS 10.15+ (Intel 处理器)
- **主程序**: `MyKeyVault.Finance.CLI`

### macOS Apple Silicon
- **文件名**: `MyKeyVault.Finance.CLI-osx-arm64.zip` (29 MB)
- **适用系统**: macOS 11+ (M1/M2/M3 芯片)
- **主程序**: `MyKeyVault.Finance.CLI`

## ✨ 特性

✅ **自包含运行**：无需安装 .NET 运行时
✅ **配置保留**：appsettings.json 包含预配置的 API 信息
✅ **单文件部署**：所有依赖已打包进可执行文件
✅ **跨平台支持**：Windows、macOS Intel、macOS Apple Silicon
✅ **中文字段映射**：所有财务字段均显示官方中文名称
✅ **智能元数据**：自动提取股票代码、报告年度、报告周期

## 📋 用户使用说明

### Windows 用户

1. **解压文件**
   ```
   解压 MyKeyVault.Finance.CLI-win-x64.zip
   ```

2. **运行程序**
   - 双击 `MyKeyVault.Finance.CLI.exe`
   - 或在命令提示符中运行：
     ```cmd
     MyKeyVault.Finance.CLI.exe
     ```

3. **首次运行可能遇到的问题**
   - 如果 Windows 显示 SmartScreen 警告，点击"更多信息" → "仍要运行"

### macOS 用户

1. **解压文件**
   ```bash
   unzip MyKeyVault.Finance.CLI-osx-x64.zip  # Intel 版本
   # 或
   unzip MyKeyVault.Finance.CLI-osx-arm64.zip  # Apple Silicon 版本
   ```

2. **运行程序**
   ```bash
   cd osx-x64  # 或 osx-arm64
   ./MyKeyVault.Finance.CLI
   ```

3. **首次运行可能遇到的问题**
   - 如果提示"无法打开，因为无法验证开发者"：
     - 方法1: 右键点击程序 → 选择"打开" → 点击"打开"
     - 方法2: 系统偏好设置 → 安全性与隐私 → 点击"仍要打开"
   
   - 如果提示权限问题：
     ```bash
     chmod +x MyKeyVault.Finance.CLI
     ```

## 🔧 配置文件说明

每个发布包都包含 `appsettings.json` 配置文件：

```json
{
  "ApiSettings": {
    "BaseUrl": "https://mykeyvault.cn",
    "AppId": "app_7d3bdc0d91634b6fa9c72eb1899ed2f4",
    "AppSecret": "1vE5U/MBX9Rwmn6C2IoNHmPGz6aqjmjm14gJZjJTz4k="
  }
}
```

**注意**：
- 用户可直接使用，无需修改配置
- 如需修改 API 地址，编辑 `BaseUrl` 字段
- 保护好 `AppSecret`，不要泄露

## 📊 导出功能

程序将导出三大财务报表到 Excel 文件：

### 导出内容
1. **资产负债表**（172 个字段）
2. **利润表**（96 个字段）
3. **现金流量表**（100 个字段）

### 导出格式
- **文件名**: `股票代码_年度_周期.xlsx`
- **布局**: 纵向展示，字段名在 A 列，数据在后续列
- **元数据**: 前 4 行显示股票代码、导出时间、报告年度、报告周期

### 使用示例
```
请输入股票代码（例如 600036）: 600036
请输入年度（例如 2024）: 2024
请输入报告期（1=一季度, 2=半年报, 3=三季度, 4=年报）: 4

正在导出...
✓ 导出成功: 600036_2024_年报.xlsx
```

## 📁 文件结构

```
发布包/
├── MyKeyVault.Finance.CLI.exe (或 MyKeyVault.Finance.CLI)
├── appsettings.json
└── README.txt
```

## 🚀 分发建议

### 发送给用户的文件

根据用户系统选择对应的 ZIP 文件：

| 用户系统 | 发送文件 | 大小 |
|---------|---------|------|
| Windows 10/11 | `MyKeyVault.Finance.CLI-win-x64.zip` | 31 MB |
| macOS Intel | `MyKeyVault.Finance.CLI-osx-x64.zip` | 31 MB |
| macOS M1/M2/M3 | `MyKeyVault.Finance.CLI-osx-arm64.zip` | 29 MB |

### 用户需要的环境

- ✅ **无需安装 .NET**：已包含所有运行时
- ✅ **无需安装 Excel**：仅生成 .xlsx 文件，不调用 Excel
- ✅ **无需网络（API 除外）**：程序本地运行，仅在获取数据时连接 API

## 🔄 版本信息

- **版本**: v1.0.0
- **发布日期**: 2025-11-18
- **.NET 版本**: .NET 8.0
- **依赖库**: ClosedXML 0.104.2 (MIT License)

## 📝 更新日志

### v1.0.0 (2025-11-18)
- ✨ 初始发布版本
- ✅ 支持三大财务报表导出
- ✅ 完整的中文字段映射
- ✅ 自包含部署，无需依赖
- ✅ 跨平台支持（Windows/macOS）

## 🛠️ 重新发布

如需更新程序，运行发布脚本：

### macOS/Linux
```bash
./publish-all.sh  # 发布所有平台
./package.sh      # 打包成 ZIP
```

### Windows
```cmd
publish-all.bat   # 发布所有平台
```

## 📞 技术支持

如有问题，请联系技术支持团队。

---

**© 2025 MyKeyVault | All Rights Reserved**
