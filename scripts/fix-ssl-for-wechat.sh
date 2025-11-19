#!/bin/bash
# 修复 nginx SSL 配置以支持微信小程序

echo '🔧 修复 nginx SSL 配置以支持微信小程序...'

# 备份当前配置
cp /etc/nginx/sites-available/mykeyvault /etc/nginx/sites-available/mykeyvault.backup_wechat_$(date +%Y%m%d_%H%M%S)
echo '✅ 已备份当前配置'

# 创建优化后的配置
cat > /etc/nginx/sites-available/mykeyvault << 'EOFNGINX'
# MyKeyVault HTTPS 配置 (优化微信小程序支持)

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
    ssl_trusted_certificate /etc/letsencrypt/live/mykeyvault.cn/chain.pem;
    
    # SSL 优化配置（微信小程序兼容）
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers 'ECDHE-RSA-AES128-GCM-SHA256:ECDHE-RSA-AES256-GCM-SHA384:ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-ECDSA-AES256-GCM-SHA384:DHE-RSA-AES128-GCM-SHA256:DHE-RSA-AES256-GCM-SHA384';
    ssl_prefer_server_ciphers on;
    ssl_session_cache shared:SSL:50m;
    ssl_session_timeout 1d;
    
    # OCSP Stapling（使用阿里云 DNS）
    ssl_stapling on;
    ssl_stapling_verify on;
    resolver 223.5.5.5 223.6.6.6 valid=300s;
    resolver_timeout 5s;
    
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
EOFNGINX

echo '✅ 配置文件已更新'

# 测试配置
echo '🧪 测试 nginx 配置...'
nginx -t

if [ $? -eq 0 ]; then
    echo '✅ Nginx 配置测试通过'
    systemctl reload nginx
    echo '✅ Nginx 已重载'
    echo ''
    echo '🔍 验证 SSL 配置:'
    openssl s_client -connect mykeyvault.cn:443 -servername mykeyvault.cn </dev/null 2>&1 | grep -A 3 'Certificate chain'
    echo ''
    echo '✅ SSL 配置优化完成！'
    echo '现在可以测试微信小程序连接了'
else
    echo '❌ Nginx 配置测试失败，回滚...'
    cp /etc/nginx/sites-available/mykeyvault.backup_wechat_* /etc/nginx/sites-available/mykeyvault
    exit 1
fi
