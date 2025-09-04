# 邮件服务配置说明

## 概述

MyKeyVault 现在支持邮件发送功能，用于：
- 用户注册时的邮箱确认
- 忘记密码时的密码重置链接
- 重新发送邮箱确认链接

## 配置方法

### 1. 环境变量配置（推荐生产环境）

设置以下环境变量：

```bash
SMTP_HOST=smtp.gmail.com              # SMTP服务器地址
SMTP_PORT=587                         # SMTP端口（默认587）
SMTP_USER=your-email@gmail.com        # SMTP用户名
SMTP_PASSWORD=your-app-password       # SMTP密码或应用专用密码
SMTP_FROM_EMAIL=your-email@gmail.com  # 发件人邮箱
SMTP_FROM_NAME=MyKeyVault            # 发件人姓名（可选）
```

### 2. 配置文件配置（开发环境）

在 `appsettings.Development.json` 中配置：

```json
{
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUser": "your-email@gmail.com",
    "SmtpPassword": "your-app-password",
    "FromEmail": "your-email@gmail.com",
    "FromName": "MyKeyVault Development",
    "ThrowOnError": false
  }
}
```

### 3. 常见邮件服务商配置

#### Gmail
- SMTP Host: `smtp.gmail.com`
- SMTP Port: `587`
- 需要启用"两步验证"并生成"应用专用密码"

#### Outlook/Hotmail
- SMTP Host: `smtp-mail.outlook.com`
- SMTP Port: `587`

#### QQ邮箱
- SMTP Host: `smtp.qq.com`
- SMTP Port: `587`
- 需要开启SMTP服务并获取授权码

#### 163邮箱
- SMTP Host: `smtp.163.com`
- SMTP Port: `465` 或 `994`

## 开发模式

如果没有配置SMTP信息，系统将：
1. 在日志中记录警告信息
2. 打印邮件内容到日志（便于开发调试）
3. 不会实际发送邮件，但不会报错

## 故障排除

### 1. 检查日志
查看应用程序日志中的邮件发送相关信息：
- 警告信息：SMTP配置不完整
- 错误信息：邮件发送失败的详细原因

### 2. 常见问题
- **535 Authentication failed**: 用户名密码错误，或需要使用应用专用密码
- **530 Must issue a STARTTLS command first**: 端口配置错误，应使用587
- **Connection timeout**: SMTP服务器地址错误或网络问题

### 3. 测试邮件发送
1. 启动应用程序
2. 尝试注册新用户或使用"忘记密码"功能
3. 检查日志输出和邮箱收件箱

## 安全建议

1. **生产环境**: 使用环境变量存储敏感信息，不要将密码写入配置文件
2. **应用专用密码**: 对于Gmail等服务，使用应用专用密码而非账户密码
3. **SSL/TLS**: 确保使用加密连接（端口587或465）
