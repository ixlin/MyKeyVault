#!/bin/bash
# 微信爬虫服务启动脚本

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# 检查虚拟环境
if [ ! -d "venv" ]; then
    echo "创建虚拟环境..."
    python3 -m venv venv
fi

# 激活虚拟环境
source venv/bin/activate

# 安装依赖
echo "检查依赖..."
pip install -q -r requirements.txt

# 检查 Playwright 浏览器
if ! python3 -c "from playwright.sync_api import sync_playwright; p = sync_playwright().start(); p.chromium.executable_path; p.stop()" 2>/dev/null; then
    echo "安装 Playwright 浏览器..."
    playwright install chromium
fi

# 启动服务
echo "启动微信爬虫服务..."
exec python3 app.py
