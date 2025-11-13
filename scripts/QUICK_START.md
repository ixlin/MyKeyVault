# 🚀 快速配置指南

## 配置项简单说明

你的 `appsettings.Development.json` 里的配置含义:

```json
{
  "Tushare": {
    "Token": "你的Tushare Pro Token",           // ← 从 tushare.pro 网站获取
    "BaseUrl": "http://api.tushare.pro",         // ← 固定值,不用改
    "JwtSecret": "服务器生成Token的密钥",       // ← 随机密码,用于生成客户端Token
    "JwtExpiresInSeconds": 7200,                 // ← Token有效期2小时
    "EncryptionKey": "加密AppSecret的密钥"      // ← 随机密码,保护数据库数据
  }
}
```

**核心流程**:
1. CLI工具用 AppId+AppSecret → 服务器验证 → 服务器用 JwtSecret 生成 Token → 返回给CLI
2. CLI用 Token → 调用查询接口 → 服务器用你的 Tushare Token → 调用官方API → 返回数据

---

## ⚡ 一键部署(推荐)

在**本地电脑**执行:

```bash
cd /Users/xionglin/GitHub/MyKeyVault
./scripts/deploy-tushare-config.sh
```

按提示输入服务器地址(例如: `root@mykeyvault.cn`),脚本会自动:
1. SSH 连接服务器
2. 配置环境变量到 systemd 服务
3. 重启服务
4. 验证配置

---

## 🔧 手动配置(如果自动部署失败)

### 步骤1: SSH 登录服务器
```bash
ssh root@mykeyvault.cn
```

### 步骤2: 编辑服务配置
```bash
sudo nano /etc/systemd/system/mykeyvault.service
```

### 步骤3: 在 `[Service]` 部分添加环境变量

找到 `[Service]` 这一行,在它**下面**添加:

```ini
Environment="TUSHARE_TOKEN=d7a5c94ae0f3a5d66d34e5e58027a5dc0a88b99d671df661ba62d21f"
Environment="TUSHARE_JWT_SECRET=dl8mK5DxJuIZLYHWk2n5n3hYgztXq/T4nYMyFGQ8Ovo="
Environment="TUSHARE_ENCRYPTION_KEY=a3ej2MYXf4C+8vRtQk8dhJT8OQbi1+9XGH7FxkHXH1Q="
```

保存退出(Ctrl+O, Enter, Ctrl+X)

### 步骤4: 重启服务
```bash
sudo systemctl daemon-reload
sudo systemctl restart mykeyvault.service
sudo systemctl status mykeyvault.service
```

---

## ✅ 验证配置

```bash
curl -X POST https://mykeyvault.cn/api/tushare/auth/token \
  -H "Content-Type: application/json" \
  -d '{"appId":"app_7d3bdc0d91634b6fa9c72eb1899ed2f4","appSecret":"1vE5U/MBX9Rwmn6C2IoNHmPGz6aqjmjm14gJZjJTz4k="}'
```

**成功示例**:
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 7200,
  "tokenType": "Bearer"
}
```

**失败示例**:
```json
{
  "error": "config_missing",
  "error_description": "JwtSecret 未配置..."
}
```
→ 说明配置未生效,检查环境变量是否正确设置

---

## 🎯 配置完成后,运行CLI工具

```bash
cd /Users/xionglin/GitHub/MyKeyVault/src/MyKeyVault.Finance.CLI
dotnet run
```

按提示输入:
- 股票代码: `600036`
- 年度: `2024`
- 报告期: `2` (半年度)

即可导出 Excel 文件!

---

## ❓ 常见问题

**Q: 为什么需要这么多配置?**
A: 
- `Token`: 调用 Tushare 官方 API 必需
- `JwtSecret`: 保护你的 API,防止未授权访问
- `EncryptionKey`: 保护数据库中存储的用户密钥

**Q: 这些密钥安全吗?**
A: 
- 存在服务器环境变量中,不会提交到 Git
- 生产环境建议重新生成随机密钥:
  ```bash
  openssl rand -base64 32  # 生成新密钥
  ```

**Q: CLI工具需要配置什么?**
A: 
- 只需配置 `appsettings.json` 里的 AppId 和 AppSecret
- 已经帮你配置好了:
  - AppId: `app_7d3bdc0d91634b6fa9c72eb1899ed2f4`
  - AppSecret: `1vE5U/MBX9Rwmn6C2IoNHmPGz6aqjmjm14gJZjJTz4k=`
