# 📦 分发文件清单

## 发布位置
```
src/MyKeyVault.Finance.CLI/publish/
```

## 📌 分发给用户的 ZIP 文件

### 1️⃣ Windows 用户
**文件**: `MyKeyVault.Finance.CLI-win-x64.zip` (31 MB)
- ✅ 包含可执行文件 `MyKeyVault.Finance.CLI.exe`
- ✅ 包含配置文件 `appsettings.json`
- ✅ 包含使用说明 `README.txt`

### 2️⃣ macOS Intel 用户
**文件**: `MyKeyVault.Finance.CLI-osx-x64.zip` (31 MB)
- ✅ 包含可执行文件 `MyKeyVault.Finance.CLI`
- ✅ 包含配置文件 `appsettings.json`
- ✅ 包含使用说明 `README.txt`

### 3️⃣ macOS Apple Silicon 用户（M1/M2/M3）
**文件**: `MyKeyVault.Finance.CLI-osx-arm64.zip` (29 MB)
- ✅ 包含可执行文件 `MyKeyVault.Finance.CLI`
- ✅ 包含配置文件 `appsettings.json`
- ✅ 包含使用说明 `README.txt`

## ✅ 验证清单

- [x] 所有平台可执行文件已生成
- [x] appsettings.json 已包含在所有版本中
- [x] 配置信息正确（BaseUrl, AppId, AppSecret）
- [x] 所有版本已打包成 ZIP
- [x] 已生成用户使用说明（README.txt）
- [x] macOS 版本已设置执行权限

## 🎯 用户使用流程

1. **选择对应平台的 ZIP 文件**
2. **解压到任意目录**
3. **双击运行或命令行运行**
4. **按提示输入股票代码、年度、期数**
5. **在当前目录获得 Excel 文件**

## 📊 导出文件示例

用户输入：
- 股票代码: 600036
- 年度: 2024
- 报告期: 4（年报）

生成文件: `600036_2024_年报.xlsx`

## 🔐 安全说明

- ⚠️ ZIP 文件包含 API 密钥，请通过安全渠道传输
- ⚠️ 建议仅分发给授权用户
- ⚠️ 用户应妥善保管配置文件

## 📍 文件下载链接

将以下文件上传到文件共享服务或直接发送给用户：

```
📁 MyKeyVault.Finance.CLI-win-x64.zip      (31 MB)
📁 MyKeyVault.Finance.CLI-osx-x64.zip      (31 MB) 
📁 MyKeyVault.Finance.CLI-osx-arm64.zip    (29 MB)
📄 README.txt                               (1 KB)
```

## 🚀 快速测试

在分发前建议测试：

### Windows
```cmd
unzip MyKeyVault.Finance.CLI-win-x64.zip
cd win-x64
MyKeyVault.Finance.CLI.exe
```

### macOS
```bash
unzip MyKeyVault.Finance.CLI-osx-arm64.zip
cd osx-arm64
./MyKeyVault.Finance.CLI
```

---

**准备就绪！现在可以分发给用户使用。**
