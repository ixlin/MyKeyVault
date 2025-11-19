#!/bin/bash

################################################################################
# Let's Encrypt SSL 证书自动化配置脚本
# 功能：安装 certbot，申请证书，配置自动续期
# 适用：Ubuntu/Debian 系统 + Nginx
# 域名：mykeyvault.cn
################################################################################

set -e  # 遇到错误立即退出

# 颜色输出
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# 域名配置
DOMAIN="mykeyvault.cn"
EMAIL="sfrost@qq.com"  # 请修改为你的邮箱，用于接收证书到期提醒

# 目录配置
NGINX_CONF="/etc/nginx/sites-available/mykeyvault"
SSL_DIR="/etc/letsencrypt/live/$DOMAIN"

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Let's Encrypt SSL 自动化配置脚本${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""

# 1. 检查是否以 root 运行
if [ "$EUID" -ne 0 ]; then 
    echo -e "${RED}错误：请使用 root 权限运行此脚本${NC}"
    echo "使用: sudo bash $0"
    exit 1
fi

# 2. 检查域名解析
echo -e "${YELLOW}[1/8] 检查域名解析...${NC}"
DOMAIN_IP=$(dig +short $DOMAIN | tail -1)
SERVER_IP=$(curl -s ifconfig.me)

if [ -z "$DOMAIN_IP" ]; then
    echo -e "${RED}错误：域名 $DOMAIN 无法解析${NC}"
    exit 1
fi

echo "域名 IP: $DOMAIN_IP"
echo "服务器 IP: $SERVER_IP"

if [ "$DOMAIN_IP" != "$SERVER_IP" ]; then
    echo -e "${YELLOW}警告：域名IP与服务器IP不匹配！${NC}"
    read -p "是否继续？(y/n) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        exit 1
    fi
fi

# 3. 更新系统并安装 certbot
echo -e "${YELLOW}[2/8] 安装 certbot...${NC}"
apt-get update -qq
apt-get install -y certbot python3-certbot-nginx

# 4. 备份现有 nginx 配置
echo -e "${YELLOW}[3/8] 备份 nginx 配置...${NC}"
if [ -f "$NGINX_CONF" ]; then
    cp "$NGINX_CONF" "$NGINX_CONF.backup_$(date +%Y%m%d_%H%M%S)"
    echo "已备份到: $NGINX_CONF.backup_$(date +%Y%m%d_%H%M%S)"
fi

# 5. 修改 nginx 配置以支持 certbot 验证
echo -e "${YELLOW}[4/8] 配置 nginx 以支持 Let's Encrypt 验证...${NC}"

# 创建 HTTP 配置（用于证书验证）
cat > /etc/nginx/sites-available/mykeyvault-http-only << 'EOF'
# HTTP 配置 - 仅用于 Let's Encrypt 证书验证
server {
    listen 80;
    listen [::]:80;
    server_name mykeyvault.cn;

    # Let's Encrypt 验证路径
    location /.well-known/acme-challenge/ {
        root /var/www/html;
    }

    # 其他请求暂时返回维护页面
    location / {
        return 503;
    }
}
EOF

# 临时启用 HTTP-only 配置
ln -sf /etc/nginx/sites-available/mykeyvault-http-only /etc/nginx/sites-enabled/mykeyvault
nginx -t && systemctl reload nginx

# 6. 申请 Let's Encrypt 证书
echo -e "${YELLOW}[5/8] 申请 Let's Encrypt 证书...${NC}"
echo "域名: $DOMAIN"
echo "邮箱: $EMAIL"
echo ""

# 使用 webroot 方式申请证书（只申请主域名）
certbot certonly --webroot \
    -w /var/www/html \
    -d $DOMAIN \
    --email $EMAIL \
    --agree-tos \
    --no-eff-email \
    --non-interactive

if [ $? -ne 0 ]; then
    echo -e "${RED}证书申请失败！${NC}"
    echo "请检查："
    echo "1. 域名是否正确解析到本服务器"
    echo "2. 80 端口是否对外开放"
    echo "3. 防火墙是否阻止了访问"
    exit 1
fi

echo -e "${GREEN}✓ 证书申请成功！${NC}"

# 7. 创建完整的 HTTPS nginx 配置
echo -e "${YELLOW}[6/8] 配置 nginx HTTPS...${NC}"

cat > /etc/nginx/sites-available/mykeyvault << 'EOF'
# MyKeyVault HTTPS 配置 (Let's Encrypt)

# HTTP 重定向到 HTTPS
server {
    listen 80;
    listen [::]:80;
    server_name mykeyvault.cn;

    # Let's Encrypt 验证路径（续期时需要）
    location /.well-known/acme-challenge/ {
        root /var/www/html;
    }

    # 其他请求重定向到 HTTPS
    location / {
        return 301 https://$server_name$request_uri;
    }
}

# HTTPS 主配置
server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name mykeyvault.cn;

    # Let's Encrypt SSL 证书配置
    ssl_certificate /etc/letsencrypt/live/mykeyvault.cn/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/mykeyvault.cn/privkey.pem;
    
    # SSL 优化配置（由 Let's Encrypt 推荐）
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers ECDHE-RSA-AES128-GCM-SHA256:ECDHE-RSA-AES256-GCM-SHA384:DHE-RSA-AES128-GCM-SHA256:DHE-RSA-AES256-GCM-SHA384;
    ssl_prefer_server_ciphers on;
    ssl_session_cache shared:SSL:10m;
    ssl_session_timeout 10m;
    ssl_stapling on;
    ssl_stapling_verify on;
    
    # 信任 Let's Encrypt 的证书链
    ssl_trusted_certificate /etc/letsencrypt/live/mykeyvault.cn/chain.pem;
    
    # 安全头设置
    add_header Strict-Transport-Security "max-age=63072000; includeSubDomains; preload" always;
    add_header X-Content-Type-Options nosniff always;
    add_header X-Frame-Options DENY always;
    add_header X-XSS-Protection "1; mode=block" always;
    add_header Referrer-Policy "strict-origin-when-cross-origin" always;

    # 日志配置
    access_log /var/log/nginx/mykeyvault/access.log;
    error_log /var/log/nginx/mykeyvault/error.log;

    # 客户端上传限制
    client_max_body_size 10M;

    # 代理到 ASP.NET Core 应用
    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host $server_name;
        proxy_cache_bypass $http_upgrade;
        
        # 超时设置
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
        
        # 微信小程序 CORS 设置
        if ($request_method = 'OPTIONS') {
            add_header Access-Control-Allow-Origin *;
            add_header Access-Control-Allow-Methods "GET, POST, PUT, DELETE, OPTIONS";
            add_header Access-Control-Allow-Headers "DNT,User-Agent,X-Requested-With,If-Modified-Since,Cache-Control,Content-Type,Range,Authorization,X-CSRF-Token";
            add_header Access-Control-Max-Age 86400;
            add_header Content-Type text/plain;
            add_header Content-Length 0;
            return 204;
        }
    }

    # 健康检查端点
    location /health {
        proxy_pass http://localhost:5000/health;
        proxy_set_header Host $host;
        access_log off;
    }

    # 静态文件缓存
    location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff|woff2|ttf|eot)$ {
        proxy_pass http://localhost:5000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        
        expires 1y;
        add_header Cache-Control "public, immutable";
    }
}
EOF

# 确保日志目录存在
mkdir -p /var/log/nginx/mykeyvault

# 启用新配置
ln -sf /etc/nginx/sites-available/mykeyvault /etc/nginx/sites-enabled/mykeyvault

# 测试配置
nginx -t

if [ $? -ne 0 ]; then
    echo -e "${RED}Nginx 配置测试失败！${NC}"
    exit 1
fi

# 重载 nginx
systemctl reload nginx
echo -e "${GREEN}✓ Nginx 配置已更新${NC}"

# 8. 配置自动续期
echo -e "${YELLOW}[7/8] 配置证书自动续期...${NC}"

# 创建续期后的钩子脚本
cat > /etc/letsencrypt/renewal-hooks/deploy/reload-nginx.sh << 'EOF'
#!/bin/bash
# 证书续期后自动重载 nginx
systemctl reload nginx
echo "$(date): SSL 证书已续期，nginx 已重载" >> /var/log/letsencrypt/renewal.log
EOF

chmod +x /etc/letsencrypt/renewal-hooks/deploy/reload-nginx.sh

# 创建日志目录
mkdir -p /var/log/letsencrypt

# 测试续期（dry run）
echo "测试证书续期流程..."
certbot renew --dry-run

if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓ 证书自动续期配置成功！${NC}"
    echo "Certbot 会在证书到期前 30 天自动续期"
else
    echo -e "${RED}续期测试失败！${NC}"
fi

# 9. 显示证书信息
echo -e "${YELLOW}[8/8] 证书安装完成！${NC}"
echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}SSL 证书配置成功！${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo "证书信息："
certbot certificates

echo ""
echo "证书文件位置："
echo "  - 完整证书链: /etc/letsencrypt/live/$DOMAIN/fullchain.pem"
echo "  - 私钥: /etc/letsencrypt/live/$DOMAIN/privkey.pem"
echo "  - 证书链: /etc/letsencrypt/live/$DOMAIN/chain.pem"
echo ""
echo "自动续期："
echo "  - Certbot 定时任务已配置（每天检查 2 次）"
echo "  - 证书到期前 30 天自动续期"
echo "  - 续期后自动重载 nginx"
echo ""
echo "手动续期命令："
echo "  sudo certbot renew"
echo ""
echo "查看续期状态："
echo "  sudo certbot certificates"
echo ""
echo "测试网站："
echo "  https://$DOMAIN"
echo ""
echo -e "${GREEN}建议：使用 https://www.ssllabs.com/ssltest/ 测试 SSL 配置${NC}"
echo ""

# 10. 验证 HTTPS 是否工作
echo "正在验证 HTTPS 连接..."
sleep 2
curl -sI https://$DOMAIN | head -5

echo ""
echo -e "${GREEN}✓ 全部完成！${NC}"
