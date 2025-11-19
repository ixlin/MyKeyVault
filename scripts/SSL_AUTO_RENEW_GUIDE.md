# Let's Encrypt SSL 证书自动化部署指南

## 🎯 解决的痛点

✅ **完全自动化** - 一条命令完成所有配置  
✅ **自动续期** - 证书到期前 30 天自动续期，无需人工干预  
✅ **免费永久** - Let's Encrypt 提供永久免费的 SSL 证书  
✅ **零维护成本** - 配置一次，终身受益

告别阿里云手动部署 SSL 证书的痛苦！

---

## 📋 前置条件

- ✅ 服务器系统：Ubuntu/Debian
- ✅ Web 服务器：Nginx
- ✅ 域名已解析到服务器 IP
- ✅ 80 和 443 端口对外开放
- ✅ 有服务器 root 权限

---

## 🚀 快速开始（推荐）

### 一键部署

在你的 **Mac 本地**执行：

```bash
cd /Users/xionglin/GitHub/MyKeyVault/release/
bash deploy-ssl.sh
```

脚本会提示你输入邮箱（用于接收证书到期提醒），然后自动完成所有配置。

**整个过程约 2-3 分钟，完全自动化！**

---

## 📝 手动部署（如果你想了解细节）

### 步骤 1：上传脚本到服务器

```bash
cd /Users/xionglin/GitHub/MyKeyVault/release/

# 使用部署密钥上传
scp -i ./mykeyvault_deploy_key ../scripts/setup-ssl-auto-renew.sh root@mykeyvault.cn:/tmp/
```

### 步骤 2：修改邮箱配置

```bash
# SSH 登录服务器
ssh -i ./mykeyvault_deploy_key root@mykeyvault.cn

# 编辑脚本，修改邮箱
nano /tmp/setup-ssl-auto-renew.sh

# 找到这一行并修改为你的邮箱：
EMAIL="your-email@example.com"  # 修改为你的真实邮箱
```

### 步骤 3：执行安装

```bash
# 在服务器上执行
cd /tmp
chmod +x setup-ssl-auto-renew.sh
sudo bash setup-ssl-auto-renew.sh
```

脚本会自动完成：
1. ✓ 检查域名解析
2. ✓ 安装 certbot
3. ✓ 备份现有 nginx 配置
4. ✓ 申请 Let's Encrypt 证书
5. ✓ 配置 nginx HTTPS
6. ✓ 设置自动续期
7. ✓ 验证配置

---

## 🔧 脚本功能详解

### 自动续期机制

- **检查频率**：每天检查 2 次（12:00 和 00:00）
- **续期时机**：证书到期前 30 天自动续期
- **续期后操作**：自动重载 nginx，无需人工干预
- **通知方式**：续期成功/失败会发邮件到你的邮箱

### 证书文件位置

```
/etc/letsencrypt/live/mykeyvault.cn/
├── fullchain.pem    # 完整证书链（nginx 使用）
├── privkey.pem      # 私钥（nginx 使用）
├── chain.pem        # 中间证书链
└── cert.pem         # 域名证书
```

### Nginx 配置变化

脚本会自动：
- 将旧的阿里云证书路径替换为 Let's Encrypt 证书路径
- 保留所有其他 nginx 配置（proxy、CORS 等）
- 优化 SSL 安全配置
- 启用 OCSP Stapling

---

## 🧪 验证部署结果

### 1. 检查证书信息

```bash
# 在服务器上执行
sudo certbot certificates
```

输出示例：
```
Certificate Name: mykeyvault.cn
  Domains: mykeyvault.cn www.mykeyvault.cn
  Expiry Date: 2026-02-17 12:00:00+00:00 (VALID: 89 days)
  Certificate Path: /etc/letsencrypt/live/mykeyvault.cn/fullchain.pem
  Private Key Path: /etc/letsencrypt/live/mykeyvault.cn/privkey.pem
```

### 2. 测试网站访问

```bash
# 在本地 Mac 执行
curl -I https://mykeyvault.cn
```

应该看到：
```
HTTP/2 200
server: nginx
...
```

### 3. 在线 SSL 测试

访问：https://www.ssllabs.com/ssltest/analyze.html?d=mykeyvault.cn

期望评分：**A 或 A+**

### 4. 浏览器验证

打开 https://mykeyvault.cn，点击地址栏的锁图标：
- ✅ 显示"连接是安全的"
- ✅ 证书颁发者：Let's Encrypt
- ✅ 有效期：90 天

---

## 🛠️ 日常维护

### 查看续期状态

```bash
sudo certbot certificates
```

### 手动触发续期（测试）

```bash
# Dry run（模拟续期，不实际更新证书）
sudo certbot renew --dry-run

# 强制续期（即使未到期）
sudo certbot renew --force-renewal
```

### 查看续期日志

```bash
# 查看 certbot 日志
sudo tail -f /var/log/letsencrypt/letsencrypt.log

# 查看续期钩子日志
sudo cat /var/log/letsencrypt/renewal.log
```

### 检查定时任务

```bash
# certbot 自动添加的 systemd timer
sudo systemctl status certbot.timer

# 查看下次运行时间
sudo systemctl list-timers certbot.timer
```

---

## 🚨 故障排除

### 问题 1：证书申请失败

**现象**：
```
Failed authorization procedure. mykeyvault.cn (http-01): 
urn:ietf:params:acme:error:connection :: Connection refused
```

**原因**：Let's Encrypt 无法访问你的 80 端口

**解决方案**：
1. 检查防火墙是否开放 80 端口
   ```bash
   sudo ufw status
   sudo ufw allow 80
   sudo ufw allow 443
   ```

2. 检查域名解析
   ```bash
   dig +short mykeyvault.cn
   curl ifconfig.me  # 查看服务器 IP
   ```

3. 检查 nginx 是否运行
   ```bash
   sudo systemctl status nginx
   sudo nginx -t
   ```

### 问题 2：续期失败

**现象**：收到邮件提醒续期失败

**原因**：通常是 80 端口被占用或配置变更

**解决方案**：
```bash
# 手动触发续期看详细错误
sudo certbot renew --dry-run

# 检查 nginx 配置
sudo nginx -t

# 确保 HTTP 验证路径可访问
curl http://mykeyvault.cn/.well-known/acme-challenge/test
```

### 问题 3：nginx 重载失败

**现象**：证书续期了但网站还是旧证书

**原因**：nginx 未自动重载

**解决方案**：
```bash
# 手动重载 nginx
sudo systemctl reload nginx

# 检查续期钩子是否有权限
sudo chmod +x /etc/letsencrypt/renewal-hooks/deploy/reload-nginx.sh

# 查看钩子日志
sudo cat /var/log/letsencrypt/renewal.log
```

### 问题 4：证书路径错误

**现象**：nginx 启动失败，提示找不到证书文件

**原因**：nginx 配置中的证书路径不正确

**解决方案**：
```bash
# 检查证书实际位置
sudo ls -la /etc/letsencrypt/live/mykeyvault.cn/

# 编辑 nginx 配置
sudo nano /etc/nginx/sites-available/mykeyvault

# 确保路径正确：
ssl_certificate /etc/letsencrypt/live/mykeyvault.cn/fullchain.pem;
ssl_certificate_key /etc/letsencrypt/live/mykeyvault.cn/privkey.pem;

# 测试并重载
sudo nginx -t
sudo systemctl reload nginx
```

### 问题 5：多个域名配置

如果你有多个域名（如 www.mykeyvault.cn），需要在申请时一起添加：

```bash
sudo certbot certonly --webroot \
    -w /var/www/html \
    -d mykeyvault.cn \
    -d www.mykeyvault.cn \
    -d api.mykeyvault.cn \  # 如果有其他子域名
    --email your@email.com \
    --agree-tos \
    --non-interactive
```

---

## 📊 与阿里云 SSL 对比

| 对比项 | 阿里云免费 SSL | Let's Encrypt |
|--------|---------------|---------------|
| 价格 | 免费（限量） | 永久免费 |
| 有效期 | 1 年 | 90 天 |
| 续期方式 | **手动下载上传** ❌ | **全自动** ✅ |
| 申请流程 | 需要验证、审核、下载 | 一条命令完成 |
| 维护成本 | 高（每年手动操作） | 零（全自动） |
| 支持通配符 | 需付费 | 支持（需 DNS 验证） |
| 证书信任度 | 高 | 同样高 |

**结论**：Let's Encrypt 在自动化方面完胜！

---

## 🔐 安全最佳实践

### 1. 定期检查证书状态

建议每月检查一次：
```bash
sudo certbot certificates
```

### 2. 启用邮件通知

确保配置了正确的邮箱，以便接收：
- 证书即将到期提醒
- 续期成功/失败通知

### 3. 备份证书

虽然可以随时重新申请，但建议定期备份：
```bash
sudo tar -czf letsencrypt-backup-$(date +%Y%m%d).tar.gz /etc/letsencrypt/
```

### 4. 监控续期日志

设置日志监控，及时发现续期失败：
```bash
sudo tail -f /var/log/letsencrypt/letsencrypt.log
```

---

## 🆘 获取帮助

### 官方资源

- Let's Encrypt 官网：https://letsencrypt.org/
- Certbot 文档：https://certbot.eff.org/
- 社区论坛：https://community.letsencrypt.org/

### 常用命令速查

```bash
# 查看证书信息
sudo certbot certificates

# 手动续期
sudo certbot renew

# 测试续期
sudo certbot renew --dry-run

# 删除证书
sudo certbot delete --cert-name mykeyvault.cn

# 查看 certbot 版本
certbot --version

# 查看 nginx 配置
sudo nginx -t

# 重载 nginx
sudo systemctl reload nginx

# 查看 nginx 日志
sudo tail -f /var/log/nginx/error.log
```

---

## 🎓 进阶配置

### 配置通配符证书

如果需要 `*.mykeyvault.cn` 通配符证书：

```bash
sudo certbot certonly --manual \
    --preferred-challenges dns \
    -d "*.mykeyvault.cn" \
    -d mykeyvault.cn \
    --agree-tos \
    --email your@email.com
```

需要手动添加 DNS TXT 记录完成验证。

### 优化 SSL 性能

在 nginx 配置中添加：

```nginx
# 启用 HTTP/2
listen 443 ssl http2;

# 会话缓存
ssl_session_cache shared:SSL:50m;
ssl_session_timeout 1d;

# 启用 OCSP Stapling
ssl_stapling on;
ssl_stapling_verify on;
resolver 8.8.8.8 8.8.4.4 valid=300s;
resolver_timeout 5s;
```

### 配置 HSTS 预加载

```nginx
add_header Strict-Transport-Security "max-age=63072000; includeSubDomains; preload" always;
```

然后提交到：https://hstspreload.org/

---

## ✅ 总结

使用本方案后，你将：

1. ✅ **彻底告别手动部署 SSL** - 一次配置，终身自动
2. ✅ **零维护成本** - 证书自动续期，无需关心到期时间
3. ✅ **完全免费** - Let's Encrypt 永久免费，无限制
4. ✅ **安全可靠** - 90 天有效期，自动更新更安全
5. ✅ **行业标准** - 全球数百万网站使用 Let's Encrypt

**现在就执行 `bash deploy-ssl.sh`，享受自动化带来的便利吧！** 🎉

---

## 📞 联系支持

如果部署过程中遇到问题，可以：
1. 查看本文档的"故障排除"章节
2. 检查服务器日志：`/var/log/letsencrypt/letsencrypt.log`
3. 访问 Let's Encrypt 社区寻求帮助

---

**版本**: v1.0  
**更新日期**: 2025年11月19日  
**适用系统**: Ubuntu 20.04+, Debian 10+  
**测试状态**: ✅ 已在阿里云服务器测试通过
