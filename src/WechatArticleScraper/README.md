# 微信公众号文章爬取工具

一个用于爬取微信公众号文章的 Python 工具，能够高度还原文章内容、样式和图片。

## 功能特性

- ✅ 完整爬取文章标题、作者、发布时间
- ✅ 保留原文样式（字体颜色、加粗、引用块、缩进等）
- ✅ 自动下载原图（非压缩版本）
- ✅ 处理懒加载图片
- ✅ 视频内容引用保留（链接形式）
- ✅ 输出简洁的 HTML 格式
- ✅ 生成元数据 JSON 文件

## 环境要求

- Python 3.8+
- macOS / Linux / Windows

## 安装

### 1. 创建虚拟环境（推荐）

```bash
cd src/WechatArticleScraper
python3 -m venv venv
source venv/bin/activate  # macOS/Linux
# 或 venv\Scripts\activate  # Windows
```

### 2. 安装依赖

```bash
pip install -r requirements.txt
```

### 3. 安装 Playwright 浏览器

```bash
playwright install chromium
```

## 使用方法

### 基本用法

```bash
python scraper.py <微信公众号文章链接>
```

### 指定输出目录

```bash
python scraper.py -o ./my_articles <微信公众号文章链接>
```

### 示例

```bash
python scraper.py https://mp.weixin.qq.com/s/x_gisj2FZdorl07_M4Loww
```

## 输出结构

```
output/
├── 文章标题_20231205_143000.html    # 完整的 HTML 文章
├── 文章标题_20231205_143000_meta.json  # 元数据
└── images/                           # 图片目录
    ├── img_001_abc12345.jpg
    ├── img_002_def67890.png
    └── ...
```

## 输出示例

### HTML 文件
- 保留原文样式（颜色、字体、缩进等）
- 图片自动替换为本地路径
- 响应式布局，适配各种屏幕

### 元数据 JSON
```json
{
  "title": "文章标题",
  "author": "公众号名称",
  "publish_time": "2025-12-05 12:00:00",
  "source_url": "https://mp.weixin.qq.com/s/...",
  "scrape_time": "2025-12-05T14:30:00",
  "images_count": 10,
  "videos_count": 1
}
```

## 技术实现

1. **Playwright 无头浏览器**：完整渲染页面，处理 JavaScript 动态内容
2. **自动滚动**：触发懒加载图片
3. **原图下载**：解析微信图片 URL，获取未压缩的原图
4. **样式保留**：智能提取并保留重要的 CSS 样式
5. **防盗链处理**：正确设置请求头，绕过图片防盗链

## 注意事项

1. 请遵守微信公众平台的使用条款
2. 仅用于个人学习和研究目的
3. 请勿用于商业用途或大规模爬取
4. 部分文章可能需要登录才能完整访问

## 常见问题

### Q: 图片下载失败？
A: 可能是网络问题或防盗链限制，工具会自动重试。

### Q: 页面加载超时？
A: 检查网络连接，或尝试增加超时时间。

### Q: 视频无法下载？
A: 目前视频仅保留引用链接，完整下载功能将在后续版本支持。

## 后续计划

- [ ] 视频下载支持
- [ ] 批量爬取功能
- [ ] Markdown 输出格式
- [ ] PDF 导出
- [ ] GUI 界面

## 许可证

MIT License
