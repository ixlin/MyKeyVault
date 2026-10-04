# 邮箱验证码重置密码

登录页新增「忘记密码」，入口 `/Identity/Account/ForgotPassword`。
输入注册邮箱 → 发送验证码 → 输入六位验证码、新密码及确认密码 → 重置 → 返回登录。
不改变密码本加密密钥或条目；使用 Identity 的正式重置接口更新密码哈希和 security stamp，不直接写密码哈希。

## 邮件配置

生产服务原先没有 SMTP 配置，需要先补齐。配置在服务器私有配置或 systemd 环境文件，禁止把真实授权码写进仓库。

```ini
Email__SmtpHost=smtp.qq.com
Email__SmtpPort=465
Email__SmtpUser=发件邮箱
Email__SmtpPassword=SMTP授权码
Email__FromEmail=发件邮箱
Email__FromName=我的密码本
```

QQ 邮箱要先启用 SMTP，使用授权码而不是邮箱登录密码。465 使用直接 TLS，其他端口强制 STARTTLS，保留证书校验。邮件发送限时 20 秒。未配置时页面明确提示，禁止跳过发送后声称成功。
返回文案不披露邮箱是否注册；SMTP 异常只记录异常类型，不记录邮箱、验证码、邮件正文、密码或重置令牌。实际投递失败保留旧验证码并告警，页面提示检查邮箱，未收到请稍后重试或联系管理员。

## 安全与状态

- 六位随机验证码，10 分钟有效，单次使用；新邮件验证码替换旧验证码。
- 每个邮箱至少间隔 60 秒，每小时最多发送 5 次；单个验证码最多尝试 5 次，小时窗口最多错误 10 次。成功消费后仍保留发送限额。
- 状态复用 `AspNetUserTokens`，不需要新增表或迁移。验证码只保存随机盐哈希；整份状态含 Identity 重置令牌再用 Data Protection 加密，绑定用户 ID。
- PostgreSQL 事务级 advisory lock 串行化同一邮箱的发送与重置，阻止多实例并发重放；重置、解锁、消费验证码、审计同一事务。
- HTTP POST 每 IP 每 10 分钟最多 20 次，CSRF 防护、no-store、CSP；不信任客户端随意传入的 IP。
- 成功后清零登录失败次数、清除临时锁定，保留全站登录保护；不自动登录。其他设备的身份验证 cookie 在下一次请求且距离上次校验超过一分钟时失效。
- 数据库和 Data Protection 密钥目录都要备份，生产发布时保留原密钥目录。丢失密钥只使待用验证码失效，不改变密码本密钥。
- 注册历史上直接把 EmailConfirmed 设为 true（旧功能），本次不扩大注册流程修改；所有密码重置仍必须真正接收邮箱验证码。

参考：[OWASP 密码找回安全指南](https://cheatsheetseries.owasp.org/cheatsheets/Forgot_Password_Cheat_Sheet.html)、[ASP.NET Core Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-8.0)。

## 验证与部署（2026-10-04）

- `tests/PasswordRecovery` 在专用 PostgreSQL 数据库完成 21 项测试，包括并发只能消费一次、重放、过期、密码策略、解锁、发送限额及 SMTP 失败。
- 测试程序只接受数据库名以 `recovery_test` 开头的连接，通过 `RECOVERY_TEST_DB` 环境变量传入；不可使用真实用户数据库。
- 线上程序/数据库/原 Data Protection 密钥已备份在 `/var/backups/mykeyvault/password-recovery-20261004/`（root 私有），发布前后用户、条目及密文数量相同，原密钥文件 SHA256 一致。
- 线上入口 HTTP 200、健康检查成功。邮件仍待配置，未发送真实验证码、未修改任何真实用户密码。
