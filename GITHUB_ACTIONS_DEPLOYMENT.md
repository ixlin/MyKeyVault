# GitHub Actions 安全部署配置指南

## 问题
在使用GitHub Actions部署时，需要配置SMTP邮件服务，但不想将敏感信息（如邮箱密码）上传到GitHub仓库。

## 解决方案：使用GitHub Secrets

### 1. 设置GitHub Secrets

在GitHub仓库中设置secrets：

1. 进入你的GitHub仓库
2. 点击 **Settings** → **Secrets and variables** → **Actions**
3. 点击 **New repository secret**
4. 添加以下secrets：

```
SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_USER=your-email@gmail.com
SMTP_PASSWORD=your-app-password
SMTP_FROM_EMAIL=your-email@gmail.com
SMTP_FROM_NAME=MyKeyVault
```

### 2. 修改GitHub Actions工作流

在你的 `.github/workflows/deploy.yml` 文件中使用这些secrets：

```yaml
name: Deploy to Production

on:
  push:
    branches: [ main ]

jobs:
  deploy:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'
    
    - name: Build
      run: dotnet build --configuration Release
    
    - name: Deploy to server
      env:
        # 数据库配置
        DB_HOST: ${{ secrets.DB_HOST }}
        DB_PORT: ${{ secrets.DB_PORT }}
        DB_NAME: ${{ secrets.DB_NAME }}
        DB_USER: ${{ secrets.DB_USER }}
        DB_PASS: ${{ secrets.DB_PASS }}
        
        # 邮件配置
        SMTP_HOST: ${{ secrets.SMTP_HOST }}
        SMTP_PORT: ${{ secrets.SMTP_PORT }}
        SMTP_USER: ${{ secrets.SMTP_USER }}
        SMTP_PASSWORD: ${{ secrets.SMTP_PASSWORD }}
        SMTP_FROM_EMAIL: ${{ secrets.SMTP_FROM_EMAIL }}
        SMTP_FROM_NAME: ${{ secrets.SMTP_FROM_NAME }}
      run: |
        # 你的部署脚本
        # 这些环境变量会在部署时设置
```

### 3. Docker部署配置

如果使用Docker，在docker-compose.yml中使用环境变量：

```yaml
version: '3.8'
services:
  web:
    build: .
    environment:
      - ConnectionStrings__DefaultConnection=Host=${DB_HOST};Port=${DB_PORT};Database=${DB_NAME};Username=${DB_USER};Password=${DB_PASS}
      - Email__SmtpHost=${SMTP_HOST}
      - Email__SmtpPort=${SMTP_PORT}
      - Email__SmtpUser=${SMTP_USER}
      - Email__SmtpPassword=${SMTP_PASSWORD}
      - Email__FromEmail=${SMTP_FROM_EMAIL}
      - Email__FromName=${SMTP_FROM_NAME}
    ports:
      - "5000:8080"
```

### 4. 服务器环境变量配置

如果直接部署到服务器，可以在服务器上设置环境变量：

```bash
# 在服务器上创建环境变量文件
sudo nano /etc/systemd/system/mykeyvault.service

[Unit]
Description=MyKeyVault Web Application
After=network.target

[Service]
Type=notify
WorkingDirectory=/path/to/your/app
ExecStart=/usr/bin/dotnet MyKeyVault.Web.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=mykeyvault

# 环境变量配置
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DB_HOST=your-db-host
Environment=DB_PORT=5432
Environment=DB_NAME=mykeyvault
Environment=DB_USER=your-db-user
Environment=DB_PASS=your-db-password
Environment=SMTP_HOST=smtp.gmail.com
Environment=SMTP_PORT=587
Environment=SMTP_USER=your-email@gmail.com
Environment=SMTP_PASSWORD=your-app-password
Environment=SMTP_FROM_EMAIL=your-email@gmail.com
Environment=SMTP_FROM_NAME=MyKeyVault

[Install]
WantedBy=multi-user.target
```

### 5. 云平台配置示例

#### Azure App Service
```bash
az webapp config appsettings set --resource-group myResourceGroup --name myapp --settings \
    "Email__SmtpHost=smtp.gmail.com" \
    "Email__SmtpPort=587" \
    "Email__SmtpUser=your-email@gmail.com" \
    "Email__SmtpPassword=your-app-password"
```

#### AWS Elastic Beanstalk
在 `.ebextensions/environment.config` 中：
```yaml
option_settings:
  aws:elasticbeanstalk:application:environment:
    Email__SmtpHost: smtp.gmail.com
    Email__SmtpPort: 587
    Email__SmtpUser: your-email@gmail.com
    Email__SmtpPassword: your-app-password
```

### 6. 安全最佳实践

1. **使用应用专用密码**：
   - Gmail: 启用两步验证，生成应用专用密码
   - Outlook: 使用应用密码而非账户密码

2. **最小权限原则**：
   - 只给部署用的邮箱账户必要的权限
   - 考虑使用专门的SMTP服务（如SendGrid、AWS SES）

3. **定期轮换密钥**：
   - 定期更新SMTP密码
   - 监控异常的邮件发送活动

4. **环境隔离**：
   - 开发环境和生产环境使用不同的SMTP配置
   - 测试环境可以使用邮件捕获工具（如MailHog）

### 7. 验证配置

部署后，通过应用程序日志验证邮件发送功能：

```bash
# 查看应用程序日志
sudo journalctl -u mykeyvault -f

# 或者查看Docker日志
docker logs mykeyvault-web -f
```

查看日志中是否有我们新增的邮件发送相关信息：
- `📧 [EMAIL] 开始发送邮件`
- `⚠️ [EMAIL] SMTP配置不完整` 
- `✅ [EMAIL] 邮件发送成功`

这样既保证了安全性，又能正常使用邮件功能。
