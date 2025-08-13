# MyKeyVault 发布到 GitHub 的配置指引

为避免泄露数据库连接等敏感信息，本仓库已：
- 使用 .gitignore 忽略实际的 `appsettings*.json` 与本地 `*.db` 文件；
- 提供 `appsettings.json.sample` 与 `appsettings.Development.json.sample` 作为示例配置；
- 程序运行时仍按 ASP.NET Core 默认顺序读取配置（环境变量 > 用户密钥 > appsettings.json 等）。

## 使用方式

1) 复制示例配置
- 将 `src/MyKeyVault.Web/appsettings.json.sample` 复制为 `appsettings.json`，按需填写连接字符串；
- 可选：将 `appsettings.Development.json.sample` 复制为 `appsettings.Development.json` 并本地修改；
- 这些实际文件已被 .gitignore 忽略，不会被提交。

2) 环境变量方式（推荐在生产环境）
- 设置以下环境变量（示例）：
  - `DB_HOST`、`DB_PORT`、`DB_NAME`、`DB_USER`、`DB_PASS`
- `appsettings.json.sample` 中的连接字符串使用了 `${VAR}` 占位。你可以复制为实际文件，让部署环境通过环境变量注入值。

3) 使用 dotnet user-secrets（仅开发本机）
- 在 `src/MyKeyVault.Web` 目录执行一次初始化：
  - `dotnet user-secrets init`
  - `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=...;Port=5432;Database=...;Username=...;Password=..."`
- 用户密钥保存在本机，不会进 Git。

4) 运行
- 还原并构建：`dotnet restore && dotnet build`
- 运行站点：`dotnet run --project src/MyKeyVault.Web`

## 注意
- 请勿把真实的 `appsettings*.json` 或任何包含口令的文件加入版本库。
- 若需 CI/CD，请在流水线中通过机密变量注入连接字符串或单独的 `appsettings.Production.json`（不提交到仓库）。
