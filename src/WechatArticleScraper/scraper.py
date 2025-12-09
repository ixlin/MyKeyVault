#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
微信公众号文章爬取工具
功能：爬取微信公众号文章的标题、正文、图片、视频等内容
输出：简洁的 HTML 格式，保留原有样式
"""

import os
import re
import sys
import json
import hashlib
import argparse
from typing import Optional
from urllib.parse import urljoin, urlparse, parse_qs
from datetime import datetime

import requests
from bs4 import BeautifulSoup, NavigableString
from playwright.sync_api import sync_playwright, TimeoutError as PlaywrightTimeout


class WechatArticleScraper:
    """微信公众号文章爬取器"""
    
    def __init__(self, output_dir: str = "output"):
        self.output_dir = output_dir
        self.images_dir = os.path.join(output_dir, "images")
        # 预先计算可公开访问的基础路径（/wechat-articles/.../），用于生成绝对图片链接
        self.public_base_url = self._compute_public_base_url()
        self.session = requests.Session()
        self.session.headers.update({
            'User-Agent': 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
        })
        
        # 创建输出目录
        os.makedirs(self.images_dir, exist_ok=True)
        
        # 图片URL映射 (原始URL -> 本地路径)
        self.image_map = {}

    def _compute_public_base_url(self) -> Optional[str]:
        """根据输出目录推导可通过 Web 访问的基础路径。例如 /wechat-articles/{user}/{article}/"""
        # 期望输出目录形如 /.../wwwroot/wechat-articles/{user}/{article}
        normalized = os.path.normpath(self.output_dir)
        marker = f"{os.sep}wwwroot{os.sep}"
        if marker in normalized:
            suffix = normalized.split(marker, 1)[1]
            # 使用 URL 友好的分隔符
            suffix = suffix.replace(os.sep, "/").strip("/")
            return f"/{suffix}/"
        return None
        
    def scrape(self, url: str) -> dict:
        """
        爬取微信公众号文章
        
        Args:
            url: 微信公众号文章链接
            
        Returns:
            dict: 包含文章信息的字典
        """
        print(f"🚀 开始爬取: {url}")
        
        # 定义 PDF 输出路径
        pdf_filename = "article.pdf"
        pdf_path = os.path.join(self.output_dir, pdf_filename)
        
        # 使用 Playwright 获取完整渲染后的页面，并保存 PDF
        html_content = self._fetch_with_playwright(url, pdf_path)
        
        if not html_content:
            raise Exception("无法获取页面内容")
        
        # 解析文章内容
        article = self._parse_article(html_content, url)
        
        # 下载图片
        self._download_images(article)
        
        # 生成输出 HTML
        output_html = self._generate_html(article)
        
        # 保存文件
        filename = self._save_article(article, output_html)
        
        print(f"✅ 爬取完成: {filename}")
        
        return {
            "title": article["title"],
            "author": article["author"],
            "publish_time": article["publish_time"],
            "output_file": filename,
            "pdf_file": pdf_filename,
            "images_count": len(self.image_map),
            "videos_count": len(article.get("videos", []))
        }
    
    def _fetch_with_playwright(self, url: str, pdf_path: Optional[str] = None) -> str:
        """使用 Playwright 获取完整渲染后的页面"""
        print("📖 正在加载页面...")
        
        html_content = None
        
        with sync_playwright() as p:
            # 启动浏览器（增加反检测与稳定性参数）
            print("  启动浏览器...")
            browser = p.chromium.launch(
                headless=True,
                args=[
                    "--disable-blink-features=AutomationControlled",
                    "--no-sandbox",
                    "--disable-dev-shm-usage",
                    "--disable-gpu",
                    "--disable-infobars",
                ]
            )
            context = browser.new_context(
                viewport={'width': 1280, 'height': 800},
                user_agent='Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'
            )
            # 反检测处理：移除 webdriver 痕迹、语言等
            context.add_init_script(
                "Object.defineProperty(navigator, 'webdriver', {get: () => undefined});"
            )
            context.add_init_script(
                "window.chrome = {runtime: {}};"
            )
            context.add_init_script(
                "Object.defineProperty(navigator, 'languages', {get: () => ['zh-CN','zh','en-US','en']});"
            )
            context.add_init_script(
                "Object.defineProperty(navigator, 'plugins', {get: () => [1,2,3,4,5]});"
            )
            page = context.new_page()
            
            try:
                # 访问页面 - 降低超时要求
                print(f"  访问 URL: {url[:50]}...")
                page.goto(url, wait_until='domcontentloaded', timeout=30000)
                print("  页面已加载")
                
                # 等待文章内容加载 - 缩短超时
                print("  等待文章内容...")
                page.wait_for_selector('#js_content', timeout=8000)
                print("  文章内容已加载")
                
                # 滚动页面以触发懒加载
                self._scroll_page(page)
                
                # 等待图片加载
                page.wait_for_timeout(1500)
                
                # 保存 PDF (如果指定了路径)
                if pdf_path:
                    print(f"  📄 保存 PDF: {os.path.basename(pdf_path)}...")
                    try:
                        page.pdf(path=pdf_path, format="A4", print_background=True, margin={"top": "1cm", "bottom": "1cm", "left": "1cm", "right": "1cm"})
                        print("  ✓ PDF 保存成功")
                    except Exception as e:
                        print(f"  ⚠️ PDF 保存失败: {e}")

                # 获取页面 HTML
                html_content = page.content()
                print("  ✓ 页面获取成功")
                
            except PlaywrightTimeout as e:
                print(f"  ⚠️ 超时: {str(e)}")
                print("  尝试获取当前页面内容...")
                try:
                    html_content = page.content()
                    if html_content and len(html_content) > 1000:
                        print("  ✓ 成功获取部分内容")
                    else:
                        print("  ✗ 页面内容不完整")
                except Exception as inner_e:
                    print(f"  ✗ 获取失败: {inner_e}")
            except Exception as e:
                print(f"  ✗ 发生错误: {type(e).__name__}: {str(e)}")
                try:
                    html_content = page.content()
                except:
                    pass
            finally:
                try:
                    browser.close()
                except:
                    pass
        
        if not html_content or len(html_content) < 500:
            raise Exception("无法获取页面内容，请检查网络连接或URL是否正确")
                
        return html_content
    
    def _scroll_page(self, page):
        """滚动页面以触发懒加载图片"""
        print("  📜 滚动页面加载图片...")
        
        try:
            # 获取页面高度
            total_height = page.evaluate("document.body.scrollHeight")
            viewport_height = page.evaluate("window.innerHeight")
            
            current_position = 0
            scroll_step = viewport_height // 2
            max_scrolls = 10  # 限制最大滚动次数
            scroll_count = 0
            
            while current_position < total_height and scroll_count < max_scrolls:
                current_position += scroll_step
                page.evaluate(f"window.scrollTo(0, {current_position})")
                page.wait_for_timeout(200)
                
                # 更新页面高度（可能会因为懒加载而变化）
                new_height = page.evaluate("document.body.scrollHeight")
                if new_height == total_height:
                    scroll_count += 1
                else:
                    total_height = new_height
                    scroll_count = 0
            
            # 滚回顶部
            page.evaluate("window.scrollTo(0, 0)")
            print("  ✓ 页面滚动完成")
        except Exception as e:
            print(f"  ⚠️ 滚动失败: {e}")
    
    def _parse_article(self, html: str, url: str) -> dict:
        """解析文章内容"""
        print("🔍 解析文章内容...")
        
        soup = BeautifulSoup(html, 'lxml')
        
        # 提取标题
        title = self._extract_title(soup)
        
        # 提取作者/公众号名称
        author = self._extract_author(soup)
        
        # 提取发布时间
        publish_time = self._extract_publish_time(soup)
        
        # 提取正文内容
        content_elem = soup.select_one('#js_content')
        
        # 处理正文中的图片
        images = self._extract_images(content_elem)
        
        # 处理视频
        videos = self._extract_videos(soup)
        
        # 清理并保留样式
        cleaned_content = self._clean_content(content_elem)
        
        return {
            "title": title,
            "author": author,
            "publish_time": publish_time,
            "content": cleaned_content,
            "images": images,
            "videos": videos,
            "source_url": url
        }
    
    def _extract_title(self, soup: BeautifulSoup) -> str:
        """提取文章标题"""
        # 尝试多种选择器
        selectors = [
            '#activity-name',
            'h1.rich_media_title',
            'h2.rich_media_title',
            'meta[property="og:title"]'
        ]
        
        for selector in selectors:
            elem = soup.select_one(selector)
            if elem:
                if selector.startswith('meta'):
                    return elem.get('content', '').strip()
                return elem.get_text(strip=True)
        
        return "未知标题"
    
    def _extract_author(self, soup: BeautifulSoup) -> str:
        """提取作者/公众号名称"""
        selectors = [
            '#js_name',
            '.rich_media_meta_nickname',
            'a#js_name'
        ]
        
        for selector in selectors:
            elem = soup.select_one(selector)
            if elem:
                return elem.get_text(strip=True)
        
        return "未知作者"
    
    def _extract_publish_time(self, soup: BeautifulSoup) -> str:
        """提取发布时间"""
        # 尝试从页面脚本中提取
        scripts = soup.find_all('script')
        for script in scripts:
            if script.string and 'publish_time' in script.string:
                match = re.search(r'var\s+publish_time\s*=\s*["\'](\d+)["\']', script.string)
                if match:
                    timestamp = int(match.group(1))
                    return datetime.fromtimestamp(timestamp).strftime('%Y-%m-%d %H:%M:%S')
        
        # 尝试从 meta 标签提取
        meta_time = soup.select_one('meta[property="article:published_time"]')
        if meta_time:
            return meta_time.get('content', '')
        
        # 尝试从页面元素提取
        time_elem = soup.select_one('#publish_time')
        if time_elem:
            return time_elem.get_text(strip=True)
        
        return datetime.now().strftime('%Y-%m-%d %H:%M:%S')
    
    def _extract_images(self, content_elem) -> list:
        """提取正文中的所有图片"""
        images = []
        
        if not content_elem:
            return images
        
        for img in content_elem.find_all('img'):
            # 获取真实图片 URL（处理懒加载）
            img_url = (
                img.get('data-src') or 
                img.get('src') or 
                img.get('data-original')
            )
            
            if img_url and not img_url.startswith('data:'):
                # 获取原图 URL（去掉压缩参数或调整参数）
                original_url = self._get_original_image_url(img_url)
                images.append({
                    'original_url': original_url,
                    'display_url': img_url,
                    'alt': img.get('alt', ''),
                    'element': img
                })
        
        return images
    
    def _get_original_image_url(self, url: str) -> str:
        """获取原图 URL"""
        if 'mmbiz.qpic.cn' in url or 'mmbiz.qlogo.cn' in url:
            # 移除 /640 等尺寸限制，获取原图
            # 原始格式: https://mmbiz.qpic.cn/xxx/640?wx_fmt=jpeg
            # 原图格式: https://mmbiz.qpic.cn/xxx/0?wx_fmt=jpeg
            
            # 解析 URL
            parsed = urlparse(url)
            path = parsed.path
            
            # 将路径中的尺寸数字替换为 0（表示原图）
            # 例如 /sz_mmbiz_jpg/xxx/640 -> /sz_mmbiz_jpg/xxx/0
            path_parts = path.rsplit('/', 1)
            if len(path_parts) == 2 and path_parts[1].isdigit():
                path = path_parts[0] + '/0'
            
            # 重建 URL
            query = parsed.query
            # 确保格式参数正确
            if 'wx_fmt=' in query:
                # 保留原格式
                pass
            else:
                # 添加默认格式
                query = query + ('&' if query else '') + 'wx_fmt=png'
            
            original_url = f"{parsed.scheme}://{parsed.netloc}{path}"
            if query:
                original_url += f"?{query}"
            
            return original_url
        
        return url
    
    def _extract_videos(self, soup: BeautifulSoup) -> list:
        """提取视频信息"""
        videos = []
        
        # 微信视频通常使用 iframe 或特定的 video 标签
        # 查找 iframe 视频
        for iframe in soup.find_all('iframe', class_='video_iframe'):
            video_url = iframe.get('data-src') or iframe.get('src')
            if video_url:
                videos.append({
                    'type': 'iframe',
                    'url': video_url,
                    'element': iframe
                })
        
        # 查找腾讯视频
        for video_wrap in soup.find_all('div', class_='video_wrap'):
            video_id = video_wrap.get('data-mpvid') or video_wrap.get('data-vidtype')
            if video_id:
                videos.append({
                    'type': 'tencent',
                    'video_id': video_id,
                    'element': video_wrap
                })
        
        # 查找普通 video 标签
        for video in soup.find_all('video'):
            video_url = video.get('src')
            if video_url:
                videos.append({
                    'type': 'video',
                    'url': video_url,
                    'element': video
                })
        
        return videos
    
    def _clean_content(self, content_elem) -> str:
        """清理并保留文章样式"""
        if not content_elem:
            return ""
        
        # 复制元素以避免修改原始内容
        content = BeautifulSoup(str(content_elem), 'lxml').select_one('#js_content')
        
        if not content:
            return ""
        
        # 移除脚本和样式表标签
        for tag in content.find_all(['script', 'link']):
            tag.decompose()
        
        # 处理图片：将懒加载的 data-src 转换为 src
        for img in content.find_all('img'):
            data_src = img.get('data-src')
            if data_src and not data_src.startswith('data:'):
                # 获取原图 URL
                original_url = self._get_original_image_url(data_src)
                img['src'] = original_url
                img['data-original-src'] = data_src
            
            # 移除懒加载相关属性
            for attr in ['data-src', 'data-w', 'data-ratio', 'data-type', 'data-s']:
                if attr in img.attrs:
                    del img.attrs[attr]
            
            # 保留有用的属性
            allowed_attrs = ['src', 'alt', 'style', 'width', 'height', 'class', 'data-original-src']
            img.attrs = {k: v for k, v in img.attrs.items() if k in allowed_attrs}
        
        # 保留重要的样式属性
        self._preserve_styles(content)
        
        # 返回清理后的 HTML
        return str(content)
    
    def _preserve_styles(self, elem):
        """保留元素的重要样式"""
        # 需要保留 style 属性的标签
        styled_tags = ['p', 'span', 'div', 'section', 'strong', 'em', 'blockquote', 'h1', 'h2', 'h3', 'h4', 'h5', 'h6']
        
        for tag in elem.find_all(styled_tags):
            style = tag.get('style', '')
            
            if style:
                # 保留重要的样式属性
                important_styles = []
                style_parts = style.split(';')
                
                for part in style_parts:
                    part = part.strip()
                    if not part:
                        continue
                    
                    # 保留这些样式属性
                    keep_props = [
                        'color', 'background-color', 'background',
                        'font-weight', 'font-style', 'font-size', 'font-family',
                        'text-align', 'text-indent', 'text-decoration',
                        'line-height', 'letter-spacing',
                        'margin', 'padding',
                        'border', 'border-left', 'border-right', 'border-top', 'border-bottom',
                        'display', 'width', 'max-width'
                    ]
                    
                    for prop in keep_props:
                        if part.lower().startswith(prop):
                            important_styles.append(part)
                            break
                
                if important_styles:
                    tag['style'] = '; '.join(important_styles)
                else:
                    del tag['style']
    
    def _download_images(self, article: dict):
        """下载文章中的所有图片"""
        images = article.get('images', [])
        
        if not images:
            return
        
        print(f"📥 下载图片 ({len(images)} 张)...")
        
        for i, img_info in enumerate(images, 1):
            original_url = img_info['original_url']
            
            if original_url in self.image_map:
                continue
            
            try:
                # 下载图片
                local_path = self._download_single_image(original_url, i)
                self.image_map[original_url] = local_path
                self.image_map[img_info['display_url']] = local_path
                print(f"  ✓ [{i}/{len(images)}] 下载成功")
                
            except Exception as e:
                print(f"  ✗ [{i}/{len(images)}] 下载失败: {e}")
    
    def _download_single_image(self, url: str, index: int) -> str:
        """下载单张图片"""
        # 生成文件名
        url_hash = hashlib.md5(url.encode()).hexdigest()[:8]
        
        # 获取图片格式
        ext = self._get_image_extension(url)
        filename = f"img_{index:03d}_{url_hash}{ext}"
        filepath = os.path.join(self.images_dir, filename)
        
        # 下载图片
        headers = {
            'User-Agent': 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36',
            'Referer': 'https://mp.weixin.qq.com/',
            'Accept': 'image/webp,image/apng,image/*,*/*;q=0.8'
        }
        
        response = self.session.get(url, headers=headers, timeout=30)
        response.raise_for_status()
        
        # 检查实际的内容类型并调整扩展名
        content_type = response.headers.get('Content-Type', '')
        if 'jpeg' in content_type or 'jpg' in content_type:
            actual_ext = '.jpg'
        elif 'png' in content_type:
            actual_ext = '.png'
        elif 'gif' in content_type:
            actual_ext = '.gif'
        elif 'webp' in content_type:
            actual_ext = '.webp'
        else:
            actual_ext = ext
        
        if actual_ext != ext:
            filename = f"img_{index:03d}_{url_hash}{actual_ext}"
            filepath = os.path.join(self.images_dir, filename)
        
        with open(filepath, 'wb') as f:
            f.write(response.content)
        
        # 返回相对路径
        return os.path.join("images", filename)
    
    def _get_image_extension(self, url: str) -> str:
        """从 URL 获取图片扩展名"""
        parsed = urlparse(url)
        query = parse_qs(parsed.query)
        
        # 从 wx_fmt 参数获取格式
        wx_fmt = query.get('wx_fmt', [''])[0]
        if wx_fmt:
            return f".{wx_fmt}"
        
        # 从路径获取
        path = parsed.path.lower()
        for ext in ['.jpg', '.jpeg', '.png', '.gif', '.webp', '.bmp']:
            if ext in path:
                return ext
        
        return '.jpg'  # 默认 jpg
    
    def _generate_html(self, article: dict) -> str:
        """生成输出 HTML"""
        # 处理内容中的图片路径
        content = article['content']
        
        # 使用 DOM 方式替换图片 src，避免字符串替换遗漏
        soup = BeautifulSoup(content, 'lxml')
        for img in soup.find_all('img'):
            src_candidates = []
            if img.get('src'):
                src_candidates.append(img['src'])
            if img.get('data-original-src'):
                src_candidates.append(img['data-original-src'])
            # 去除查询参数的形式也尝试匹配，防止大小写或多余参数导致未命中
            normalized = []
            for s in src_candidates:
                normalized.append(s)
                parsed = urlparse(s)
                normalized.append(f"{parsed.scheme}://{parsed.netloc}{parsed.path}")
            local = None
            for cand in normalized:
                if cand in self.image_map:
                    local = self.image_map[cand]
                    break
            if local:
                # 始终写入相对路径，便于打包下载后离线可用；预览时依靠 <base> 修正
                img['src'] = local
                if 'data-original-src' in img.attrs:
                    del img['data-original-src']
                # 去掉懒加载类名，避免样式影响
                if 'class' in img.attrs:
                    img['class'] = [c for c in img['class'] if c not in ['lazyload', 'js_lazy']] or None
        content = str(soup)
        
        # 处理视频（第一阶段：保留原始引用）
        videos_html = ""
        if article.get('videos'):
            videos_html = self._generate_videos_html(article['videos'])
        
        # 如果在 wwwroot 下，添加 <base> 以修正预览时的相对路径；file:// 打开时通过脚本移除
        base_tag = ""
        if self.public_base_url:
            base_tag = f"    <base data-auto-remove-if-file=\"true\" href=\"{self.public_base_url}\">\n"

        # 生成完整 HTML
        html = f'''<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>{article['title']}</title>
{base_tag}    <script>
        (function() {{
            // 离线 file:// 打开时移除 base，确保图片走相对路径
            if (location.protocol === 'file:') {{
                var b = document.querySelector('base[data-auto-remove-if-file="true"]');
                if (b) {{ b.remove(); }}
            }}
        }})();
    </script>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        body {{
            font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
            line-height: 1.8;
            color: #333;
            background-color: #f5f5f5;
            padding: 20px;
        }}
        .article-container {{
            max-width: 677px;
            margin: 0 auto;
            background-color: #fff;
            padding: 30px;
            border-radius: 8px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
        }}
        .article-header {{
            margin-bottom: 30px;
            padding-bottom: 20px;
            border-bottom: 1px solid #eee;
        }}
        .article-title {{
            font-size: 24px;
            font-weight: bold;
            color: #000;
            margin-bottom: 15px;
            line-height: 1.4;
        }}
        .article-meta {{
            font-size: 14px;
            color: #888;
        }}
        .article-meta span {{
            margin-right: 20px;
        }}
        .article-content {{
            font-size: 17px;
            line-height: 1.8;
        }}
        .article-content img {{
            max-width: 100%;
            height: auto;
            display: block;
            margin: 15px auto;
            border-radius: 4px;
        }}
        .article-content p {{
            margin-bottom: 1em;
        }}
        .article-content blockquote {{
            border-left: 4px solid #ddd;
            padding-left: 15px;
            margin: 15px 0;
            color: #666;
        }}
        .article-content strong, 
        .article-content b {{
            font-weight: bold;
        }}
        .article-content em,
        .article-content i {{
            font-style: italic;
        }}
        .article-content section {{
            margin-bottom: 1em;
        }}
        .article-footer {{
            margin-top: 30px;
            padding-top: 20px;
            border-top: 1px solid #eee;
            font-size: 12px;
            color: #999;
        }}
        .article-footer a {{
            color: #576b95;
            text-decoration: none;
        }}
        .video-placeholder {{
            background: #f0f0f0;
            padding: 20px;
            text-align: center;
            margin: 15px 0;
            border-radius: 8px;
            color: #666;
        }}
        .video-placeholder a {{
            color: #576b95;
        }}
    </style>
</head>
<body>
    <div class="article-container">
        <header class="article-header">
            <h1 class="article-title">{article['title']}</h1>
            <div class="article-meta">
                <span>作者: {article['author']}</span>
                <span>发布时间: {article['publish_time']}</span>
            </div>
        </header>
        
        <article class="article-content">
            {content}
        </article>
        
        {videos_html}
        
        <footer class="article-footer">
            <p>原文链接: <a href="{article['source_url']}" target="_blank">{article['source_url']}</a></p>
            <p>抓取时间: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}</p>
        </footer>
    </div>
</body>
</html>'''
        
        return html
    
    def _generate_videos_html(self, videos: list) -> str:
        """生成视频部分的 HTML"""
        if not videos:
            return ""
        
        html_parts = ['<div class="videos-section"><h3>视频内容</h3>']
        
        for i, video in enumerate(videos, 1):
            if video['type'] == 'iframe':
                html_parts.append(f'''
                <div class="video-placeholder">
                    <p>🎬 视频 {i}</p>
                    <p><a href="{video.get('url', '#')}" target="_blank">点击观看原视频</a></p>
                </div>
                ''')
            elif video['type'] == 'tencent':
                html_parts.append(f'''
                <div class="video-placeholder">
                    <p>🎬 腾讯视频 {i}</p>
                    <p>视频ID: {video.get('video_id', '未知')}</p>
                </div>
                ''')
            else:
                html_parts.append(f'''
                <div class="video-placeholder">
                    <p>🎬 视频 {i}</p>
                    <p><a href="{video.get('url', '#')}" target="_blank">点击观看</a></p>
                </div>
                ''')
        
        html_parts.append('</div>')
        return '\n'.join(html_parts)
    
    def _save_article(self, article: dict, html_content: str) -> str:
        """保存文章到文件"""
        # 生成安全的文件名
        title = article['title']
        # 移除不安全字符
        safe_title = re.sub(r'[<>:"/\\|?*]', '', title)
        safe_title = safe_title[:50]  # 限制长度
        
        timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
        filename = f"{safe_title}_{timestamp}.html"
        filepath = os.path.join(self.output_dir, filename)
        
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(html_content)
        
        # 同时保存元数据
        meta_filepath = os.path.join(self.output_dir, f"{safe_title}_{timestamp}_meta.json")
        meta = {
            "title": article['title'],
            "author": article['author'],
            "publish_time": article['publish_time'],
            "source_url": article['source_url'],
            "scrape_time": datetime.now().isoformat(),
            "images_count": len(self.image_map),
            "videos_count": len(article.get('videos', []))
        }
        with open(meta_filepath, 'w', encoding='utf-8') as f:
            json.dump(meta, f, ensure_ascii=False, indent=2)
        
        return filename


def main():
    parser = argparse.ArgumentParser(
        description='微信公众号文章爬取工具',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog='''
示例:
  python scraper.py https://mp.weixin.qq.com/s/xxxxx
  python scraper.py -o ./my_articles https://mp.weixin.qq.com/s/xxxxx
        '''
    )
    
    parser.add_argument('url', help='微信公众号文章链接')
    parser.add_argument('-o', '--output', default='output', help='输出目录 (默认: output)')
    
    args = parser.parse_args()
    
    # 验证 URL
    if 'mp.weixin.qq.com' not in args.url:
        print("⚠️  警告: 链接可能不是微信公众号文章")
    
    try:
        scraper = WechatArticleScraper(output_dir=args.output)
        result = scraper.scrape(args.url)
        
        print("\n📊 爬取结果:")
        print(f"   标题: {result['title']}")
        print(f"   作者: {result['author']}")
        print(f"   发布时间: {result['publish_time']}")
        print(f"   图片数量: {result['images_count']}")
        print(f"   视频数量: {result['videos_count']}")
        print(f"   输出文件: {result['output_file']}")
        
    except Exception as e:
        print(f"❌ 爬取失败: {e}")
        sys.exit(1)


if __name__ == '__main__':
    main()
