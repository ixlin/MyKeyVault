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
from typing import Optional, Callable
from urllib.parse import urljoin, urlparse, parse_qs
from datetime import datetime

import requests
from bs4 import BeautifulSoup, NavigableString
from playwright.sync_api import sync_playwright, TimeoutError as PlaywrightTimeout


class WechatArticleScraper:
    """微信公众号文章爬取器"""
    
    # 超时配置（秒）
    PAGE_LOAD_TIMEOUT = 35  # 页面加载超时
    CONTENT_WAIT_TIMEOUT = 12  # 正文内容等待超时
    OVERALL_TIMEOUT = 150   # 整体任务超时（2.5分钟）
    
    def __init__(self, output_dir: str = "output", progress_callback: Optional[Callable[[int, str], None]] = None):
        self.output_dir = output_dir
        self.images_dir = os.path.join(output_dir, "images")
        self.videos_dir = os.path.join(output_dir, "videos")
        # 预先计算可公开访问的基础路径（/wechat-articles/.../），用于生成绝对图片链接
        self.public_base_url = self._compute_public_base_url()
        self.session = requests.Session()
        self.session.headers.update({
            'User-Agent': 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
        })
        
        # 创建输出目录
        os.makedirs(self.images_dir, exist_ok=True)
        os.makedirs(self.videos_dir, exist_ok=True)
        
        # 图片URL映射 (原始URL -> 本地路径)
        self.image_map = {}
        # 视频URL映射 (视频ID -> 本地路径和信息)
        self.video_map = {}
        # 捕获的视频URL列表
        self.captured_video_urls = []

        # 进度回调（用于 API/前端展示进度；CLI 也可复用）
        self._progress_callback = progress_callback
        self._last_progress = -1
        
        # 停止标志和浏览器引用（用于外部强制停止）
        self._stop_flag = False
        self._browser = None
        self._start_time = None

    def stop(self):
        """外部调用以强制停止爬取任务"""
        self._stop_flag = True
        if self._browser:
            try:
                self._browser.close()
            except Exception:
                pass
            self._browser = None

    def _check_stop(self):
        """检查是否需要停止"""
        if self._stop_flag:
            raise Exception("任务已被用户取消")

    def _check_timeout(self, stage: str = ""):
        """检查是否超时"""
        if self._start_time is None:
            return
        elapsed = (datetime.now() - self._start_time).total_seconds()
        if elapsed > self.OVERALL_TIMEOUT:
            raise Exception(f"任务超时（已运行 {int(elapsed)} 秒，超过 {self.OVERALL_TIMEOUT} 秒限制）{': ' + stage if stage else ''}")

    def _set_progress(self, percent: int, message: str):
        """上报进度：percent 0-100；message 为面向用户的阶段提示。"""
        try:
            percent = max(0, min(100, int(percent)))
        except Exception:
            percent = 0

        # 避免同一百分比重复刷屏
        if percent == self._last_progress:
            return
        self._last_progress = percent

        if self._progress_callback:
            try:
                self._progress_callback(percent, message)
            except Exception:
                pass
        else:
            # CLI 友好输出（不暴露内部细节）
            bar_len = 20
            filled = int(bar_len * percent / 100)
            bar = "#" * filled + "-" * (bar_len - filled)
            print(f"[{bar}] {percent:3d}% {message}")

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
        # 记录开始时间
        self._start_time = datetime.now()
        self._stop_flag = False
        
        print(f"🚀 开始爬取: {url}")
        self._set_progress(1, "开始获取文章")
        
        # 检查停止和超时
        self._check_stop()
        
        # 定义临时 PDF 输出路径
        temp_pdf_filename = "article_temp.pdf"
        temp_pdf_path = os.path.join(self.output_dir, temp_pdf_filename)
        
        # 使用 Playwright 获取完整渲染后的页面，并保存 PDF
        html_content = self._fetch_with_playwright(url, temp_pdf_path)
        
        if not html_content:
            raise Exception("无法获取页面内容")

        self._check_stop()
        self._check_timeout("页面内容已获取")
        self._set_progress(40, "页面内容已获取")
        
        # 解析文章内容
        article = self._parse_article(html_content, url)

        self._check_stop()
        self._check_timeout("解析文章")
        self._set_progress(50, "正在下载资源")
        
        # 下载图片
        self._download_images(article)

        self._check_stop()
        self._check_timeout("下载图片")
        self._set_progress(70, "图片处理完成")
        
        # 下载视频
        self._download_videos(article)

        self._check_stop()
        self._check_timeout("下载视频")
        self._set_progress(80, "视频处理完成")
        
        # 生成输出 HTML
        output_html = self._generate_html(article)

        self._check_stop()
        self._check_timeout("生成HTML")
        self._set_progress(90, "正在生成本地页面")
        
        # 保存文件
        filename = self._save_article(article, output_html)

        self._set_progress(95, "正在保存文件")
        
        # 重命名 PDF 文件以匹配 HTML 文件名
        pdf_filename = None
        if os.path.exists(temp_pdf_path):
            pdf_filename = filename.replace('.html', '.pdf')
            pdf_path = os.path.join(self.output_dir, pdf_filename)
            if os.path.exists(pdf_path):
                os.remove(pdf_path)
            os.rename(temp_pdf_path, pdf_path)
        
        print(f"✅ 爬取完成: {filename}")

        self._set_progress(100, "完成")
        
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
        self._set_progress(5, "启动浏览器")
        
        html_content = None
        # 重置捕获的视频URL
        self.captured_video_urls = []
        
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
            # 保存浏览器引用，以便外部可以强制关闭
            self._browser = browser
            
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
            
            # 设置请求拦截器，捕获视频URL
            def handle_request(request):
                req_url = request.url
                if '.mp4' in req_url and 'mpvideo.qpic.cn' in req_url:
                    # 提取视频ID
                    vid_match = re.search(r'vid=([^&]+)', req_url)
                    vid = vid_match.group(1) if vid_match else None
                    self.captured_video_urls.append({
                        'url': req_url,
                        'vid': vid
                    })
            
            page.on('request', handle_request)
            
            try:
                # 检查停止标志
                self._check_stop()
                
                # 访问页面 - 使用配置的页面加载超时
                print(f"  访问 URL: {url[:50]}...")
                self._set_progress(15, "加载页面")
                page.goto(url, wait_until='domcontentloaded', timeout=self.PAGE_LOAD_TIMEOUT * 1000)
                print("  页面已加载")
                
                # 检查停止标志
                self._check_stop()
                
                # 等待文章内容加载 - 缩短超时
                print("  等待文章内容...")
                self._set_progress(25, "等待文章内容")
                page.wait_for_selector('#js_content', timeout=self.CONTENT_WAIT_TIMEOUT * 1000)
                print("  文章内容已加载")
                
                # 检查停止标志
                self._check_stop()
                
                # 滚动页面以触发懒加载
                self._set_progress(30, "加载图片与视频")
                self._scroll_page(page)
                
                # 等待图片加载
                page.wait_for_timeout(1500)
                
                # 检查停止标志
                self._check_stop()
                
                # 尝试点击视频元素以触发视频URL请求
                self._trigger_video_urls(page)
                
                # 保存 PDF (如果指定了路径)
                if pdf_path:
                    print(f"  📄 保存 PDF: {os.path.basename(pdf_path)}...")
                    try:
                        self._set_progress(35, "生成预览文件")
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
                        raise Exception(f"页面加载超时（{self.PAGE_LOAD_TIMEOUT}秒）且内容不完整")
                except Exception as inner_e:
                    print(f"  ✗ 获取失败: {inner_e}")
                    raise Exception(f"页面加载超时（{self.PAGE_LOAD_TIMEOUT}秒）: {inner_e}")
            except Exception as e:
                error_msg = str(e)
                error_type = type(e).__name__
                print(f"  ✗ 发生错误: {error_type}: {error_msg}")
                
                # 如果是用户取消或超时，直接抛出
                if "取消" in error_msg or "超时" in error_msg:
                    raise
                
                # 检测常见的 Playwright 错误并提供更友好的错误信息
                if "Target page, context or browser has been closed" in error_msg:
                    raise Exception("浏览器已关闭，可能是页面加载过程中出错")
                if "net::ERR_" in error_msg:
                    raise Exception(f"网络连接错误: {error_msg}")
                if "Protocol error" in error_msg:
                    raise Exception("浏览器协议错误，请稍后重试")
                if "Timeout" in error_msg or "timeout" in error_msg:
                    raise Exception(f"操作超时: {error_msg}")
                
                # 尝试获取已加载的内容
                try:
                    html_content = page.content()
                    if html_content and len(html_content) > 1000:
                        print("  ⚠️ 出错但成功获取部分内容")
                except:
                    pass
                
                # 如果没有获取到内容，抛出原始错误
                if not html_content or len(html_content) < 500:
                    raise Exception(f"页面加载失败: {error_msg}")
            finally:
                try:
                    browser.close()
                    self._browser = None
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
    
    def _trigger_video_urls(self, page):
        """点击视频元素以触发视频URL请求"""
        try:
            # 查找视频元素
            video_spans = page.query_selector_all('span.video_iframe[data-mpvid], span[data-mpvid]')
            
            if not video_spans:
                return
            
            print(f"  🎬 发现 {len(video_spans)} 个视频，尝试获取视频链接...")
            
            for i, video_span in enumerate(video_spans):
                try:
                    # 滚动到视频元素
                    video_span.scroll_into_view_if_needed()
                    page.wait_for_timeout(500)
                    
                    # 点击视频以触发加载
                    video_span.click()
                    page.wait_for_timeout(2000)
                    
                    # 尝试关闭可能弹出的视频播放器
                    try:
                        close_btn = page.query_selector('.video_close, .close-btn, [class*="close"]')
                        if close_btn:
                            close_btn.click()
                            page.wait_for_timeout(500)
                    except:
                        pass
                    
                except Exception as e:
                    print(f"    ⚠️ 视频 {i+1} 点击失败: {e}")
            
            print(f"  ✓ 捕获到 {len(self.captured_video_urls)} 个视频URL")
            
        except Exception as e:
            print(f"  ⚠️ 触发视频URL失败: {e}")
    
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
        seen_vids = set()  # 避免重复
        
        # 1. 查找带有 data-mpvid 属性的 span 元素（微信原生视频）
        for elem in soup.find_all(attrs={'data-mpvid': True}):
            vid = elem.get('data-mpvid') or elem.get('vid')
            if vid and vid not in seen_vids:
                seen_vids.add(vid)
                # 解码封面图URL
                cover_url = elem.get('data-cover', '')
                if cover_url:
                    from urllib.parse import unquote
                    cover_url = unquote(cover_url)
                
                videos.append({
                    'type': 'wechat_native',
                    'vid': vid,
                    'cover_url': cover_url,
                    'data_src': elem.get('data-src', ''),
                    'element': elem
                })
        
        # 2. 查找 class 包含 video_iframe 的 span 元素
        for elem in soup.find_all('span', class_='video_iframe'):
            vid = elem.get('data-mpvid') or elem.get('vid')
            if vid and vid not in seen_vids:
                seen_vids.add(vid)
                cover_url = elem.get('data-cover', '')
                if cover_url:
                    from urllib.parse import unquote
                    cover_url = unquote(cover_url)
                
                videos.append({
                    'type': 'wechat_native',
                    'vid': vid,
                    'cover_url': cover_url,
                    'data_src': elem.get('data-src', ''),
                    'element': elem
                })
        
        # 3. 查找 iframe 视频（兼容旧格式）
        for iframe in soup.find_all('iframe', class_='video_iframe'):
            video_url = iframe.get('data-src') or iframe.get('src')
            if video_url:
                videos.append({
                    'type': 'iframe',
                    'url': video_url,
                    'element': iframe
                })
        
        # 4. 查找腾讯视频
        for video_wrap in soup.find_all('div', class_='video_wrap'):
            video_id = video_wrap.get('data-mpvid') or video_wrap.get('data-vidtype')
            if video_id and video_id not in seen_vids:
                seen_vids.add(video_id)
                videos.append({
                    'type': 'tencent',
                    'video_id': video_id,
                    'element': video_wrap
                })
        
        # 5. 查找普通 video 标签
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
    
    def _download_videos(self, article: dict):
        """下载文章中的所有视频"""
        videos = article.get('videos', [])
        
        if not videos:
            return
        
        # 筛选出微信原生视频
        native_videos = [v for v in videos if v.get('type') == 'wechat_native' and v.get('vid')]
        
        if not native_videos:
            print(f"📹 发现 {len(videos)} 个视频，但无可下载的微信原生视频")
            return
        
        print(f"📹 下载视频 ({len(native_videos)} 个)...")
        
        for i, video in enumerate(native_videos, 1):
            vid = video.get('vid')
            
            if vid in self.video_map:
                continue
            
            try:
                # 查找对应的视频URL
                video_url = None
                for captured in self.captured_video_urls:
                    if captured.get('vid') == vid:
                        video_url = captured.get('url')
                        break
                
                # 如果没有精确匹配，尝试使用第一个捕获的URL
                if not video_url and self.captured_video_urls and i == 1:
                    video_url = self.captured_video_urls[0].get('url')
                
                if not video_url:
                    print(f"  ⚠️ [{i}/{len(native_videos)}] 未找到视频URL (vid: {vid})")
                    continue
                
                # 下载视频
                local_video_path = self._download_single_video(video_url, vid, i)
                
                # 下载封面图
                local_cover_path = None
                cover_url = video.get('cover_url')
                if cover_url:
                    try:
                        local_cover_path = self._download_video_cover(cover_url, vid, i)
                    except Exception as e:
                        print(f"    ⚠️ 封面下载失败: {e}")
                
                self.video_map[vid] = {
                    'local_video_path': local_video_path,
                    'local_cover_path': local_cover_path,
                    'vid': vid
                }
                print(f"  ✓ [{i}/{len(native_videos)}] 视频下载成功")
                
            except Exception as e:
                print(f"  ✗ [{i}/{len(native_videos)}] 视频下载失败: {e}")
    
    def _download_single_video(self, url: str, vid: str, index: int) -> str:
        """下载单个视频文件"""
        # 生成文件名
        url_hash = hashlib.md5(vid.encode()).hexdigest()[:8]
        filename = f"video_{index:03d}_{url_hash}.mp4"
        filepath = os.path.join(self.videos_dir, filename)
        
        print(f"    📥 正在下载视频 {index}...")
        
        headers = {
            'User-Agent': 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
            'Referer': 'https://mp.weixin.qq.com/',
            'Accept': '*/*',
            'Accept-Encoding': 'identity',  # 禁用压缩，便于获取准确大小
            'Range': 'bytes=0-'  # 支持断点续传
        }
        
        # 使用流式下载
        response = self.session.get(url, headers=headers, timeout=120, stream=True)
        response.raise_for_status()
        
        # 获取文件大小
        total_size = int(response.headers.get('content-length', 0))
        
        downloaded = 0
        last_reported = -5
        with open(filepath, 'wb') as f:
            for chunk in response.iter_content(chunk_size=8192):
                if chunk:
                    f.write(chunk)
                    downloaded += len(chunk)
                    if total_size > 0:
                        percent = (downloaded / total_size) * 100
                        if percent - last_reported >= 5 or percent >= 100:
                            last_reported = percent
                            size_mb = total_size / (1024 * 1024)
                            print(f"    📊 下载进度: {percent:.1f}% ({size_mb:.1f}MB)", end='\r', flush=True)
                    else:
                        # 无 content-length 时做轻量提示
                        mb = downloaded / (1024 * 1024)
                        if int(mb) != int((downloaded - len(chunk)) / (1024 * 1024)):
                            print(f"    📊 已下载: {mb:.0f}MB", end='\r', flush=True)
        
        print(f"    📊 下载完成: 100%                    ")
        
        # 返回相对路径
        return os.path.join("videos", filename)
    
    def _download_video_cover(self, url: str, vid: str, index: int) -> str:
        """下载视频封面图"""
        url_hash = hashlib.md5(vid.encode()).hexdigest()[:8]
        
        # 从URL获取格式
        ext = '.jpg'
        if 'wx_fmt=' in url:
            fmt_match = re.search(r'wx_fmt=(\w+)', url)
            if fmt_match:
                ext = f".{fmt_match.group(1)}"
        
        filename = f"cover_{index:03d}_{url_hash}{ext}"
        filepath = os.path.join(self.images_dir, filename)
        
        headers = {
            'User-Agent': 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36',
            'Referer': 'https://mp.weixin.qq.com/',
            'Accept': 'image/webp,image/apng,image/*,*/*;q=0.8'
        }
        
        response = self.session.get(url, headers=headers, timeout=30)
        response.raise_for_status()
        
        with open(filepath, 'wb') as f:
            f.write(response.content)
        
        return os.path.join("images", filename)
    
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

        # 处理视频：在正文中原始位置替换为本地 <video>
        videos_by_vid = {}
        for v in article.get('videos', []) or []:
            vid = v.get('vid')
            if vid:
                videos_by_vid[vid] = v

        # 微信文章中视频常见结构：span.video_iframe[data-mpvid] 或任意元素[data-mpvid]
        video_elems = list(soup.find_all(attrs={'data-mpvid': True}))
        if video_elems:
            for elem in video_elems:
                vid = elem.get('data-mpvid') or elem.get('vid')
                if not vid:
                    continue

                local_info = self.video_map.get(vid, {})
                local_video = local_info.get('local_video_path')
                local_cover = local_info.get('local_cover_path')

                # 构造替换节点
                if local_video:
                    container = soup.new_tag('div')
                    container['class'] = ['video-container']
                    container['style'] = 'margin: 15px 0;'

                    video_tag = soup.new_tag('video')
                    video_tag['controls'] = ''
                    video_tag['style'] = 'max-width: 100%; border-radius: 8px; background: #000;'
                    if local_cover:
                        video_tag['poster'] = local_cover

                    source = soup.new_tag('source')
                    source['src'] = local_video
                    source['type'] = 'video/mp4'

                    video_tag.append(source)
                    video_tag.append('您的浏览器不支持视频播放')
                    container.append(video_tag)
                    elem.replace_with(container)
                else:
                    # 未下载到视频时：在原位置输出占位链接
                    v = videos_by_vid.get(vid, {})
                    data_src = v.get('data_src') or elem.get('data-src') or '#'

                    placeholder = soup.new_tag('div')
                    placeholder['class'] = ['video-placeholder']

                    p1 = soup.new_tag('p')
                    p1.string = '🎬 视频'
                    placeholder.append(p1)

                    p2 = soup.new_tag('p')
                    a = soup.new_tag('a')
                    a['href'] = data_src
                    a['target'] = '_blank'
                    a.string = '点击观看原视频'
                    p2.append(a)
                    placeholder.append(p2)

                    elem.replace_with(placeholder)

        # 处理代码块：规范化微信 code-snippet
        # 目标：保证行号数量和代码行数一致，同时尽量保留微信原始 DOM（避免把一行命令拆成多行、丢失 span 高亮）。
        for section in soup.select('section.code-snippet__fix'):
            try:
                ul = section.select_one('ul.code-snippet__line-index')
                pre = section.select_one('pre')
                if not pre:
                    continue

                codes = pre.find_all('code', recursive=False)
                if not codes:
                    codes = pre.find_all('code')
                if not codes:
                    continue

                line_count = 0
                normalized_codes = []
                for code in codes:
                    # 将 <br> 视为真正的换行（不要把 span/空格强行转换为换行）
                    for br in code.find_all('br'):
                        br.replace_with('\n')

                    # &nbsp; 还原成普通空格（在文本节点层面替换，保留高亮 span）
                    for s in code.find_all(string=True):
                        if '\xa0' in s:
                            s.replace_with(s.replace('\xa0', ' '))

                    # 统计行数：按当前 code 文本中的换行符计算
                    text = code.get_text()
                    line_count += max(1, text.count('\n') + 1)
                    normalized_codes.append(code)

                # 规范化 pre：只保留 code（移除多余的空白文本节点），但不合并成纯文本
                pre.clear()
                for code in normalized_codes:
                    pre.append(code)

                # 同步行号数量
                if ul is not None:
                    ul.clear()
                    for _ in range(max(1, line_count)):
                        ul.append(soup.new_tag('li'))
            except Exception:
                # 代码块解析失败时不影响正文输出
                continue

        # 修复：微信正文容器有时带 `visibility:hidden; opacity:0`（用于前端渐显/排版），
        # 离线 HTML 直接渲染会导致“正文完全不可见”。这里仅移除隐藏相关样式。
        js_content = soup.select_one('#js_content')
        if js_content is not None and js_content.has_attr('style'):
            style = js_content.get('style', '')
            style = re.sub(r'(?i)\bvisibility\s*:\s*hidden\s*;?', '', style)
            style = re.sub(r'(?i)\bopacity\s*:\s*0(?:\.0+)?\s*;?', '', style)
            style = style.strip()
            if style:
                js_content['style'] = style
            else:
                del js_content['style']

        # 避免把 BeautifulSoup 自动补的 <html><body> 包装串进正文
        if js_content is not None:
            content = js_content.decode_contents()
        elif soup.body is not None:
            content = soup.body.decode_contents()
        else:
            content = soup.decode_contents()
        
        # 视频已内嵌到正文中，避免重复展示
        videos_html = ""
        
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

        /* WeChat code snippet blocks */
        section.code-snippet__fix {{
            display: flex;
            border-radius: 8px;
            overflow: hidden;
            margin: 15px 0;
            background: #1f1f1f;
            font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace;
        }}
        section.code-snippet__fix .code-snippet__line-index {{
            list-style: none;
            padding: 12px 10px;
            margin: 0;
            background: #1f1f1f;
            color: rgba(255, 255, 255, 0.55);
            text-align: right;
            user-select: none;
            counter-reset: code_line;
            flex: 0 0 auto;
            font-size: 14px;
            line-height: 1.6;
        }}
        section.code-snippet__fix .code-snippet__line-index li {{
            list-style: none;
            margin: 0;
            padding: 0;
            line-height: 1.6;
            counter-increment: code_line;
        }}
        section.code-snippet__fix .code-snippet__line-index li::before {{
            content: counter(code_line);
            display: block;
            min-width: 1.5em;
        }}
        section.code-snippet__fix pre {{
            margin: 0;
            padding: 12px 14px;
            overflow: auto;
            background: #1f1f1f;
            color: #e6e6e6;
            flex: 1 1 auto;
            line-height: 1.6;
            font-size: 14px;
        }}
        section.code-snippet__fix pre code {{
            display: block;
            white-space: pre;
            margin: 0;
            padding: 0;
            line-height: 1.6;
        }}
        section.code-snippet__fix pre code span {{
            line-height: inherit;
        }}
        section.code-snippet__fix .code-snippet__meta {{
            color: rgba(255, 255, 255, 0.7);
        }}
        section.code-snippet__fix .code-snippet__keyword {{
            color: #ff9f43;
            font-weight: 600;
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
        
        html_parts = ['<div class="videos-section" style="margin-top: 20px;"><h3 style="margin-bottom: 15px;">视频内容</h3>']
        
        for i, video in enumerate(videos, 1):
            vid = video.get('vid') or video.get('video_id')
            
            # 检查是否有本地视频文件
            if vid and vid in self.video_map:
                video_info = self.video_map[vid]
                local_video = video_info.get('local_video_path', '')
                local_cover = video_info.get('local_cover_path', '')
                
                if local_video:
                    # 使用本地视频文件
                    poster_attr = f'poster="{local_cover}"' if local_cover else ''
                    html_parts.append(f'''
                    <div class="video-container" style="margin: 15px 0;">
                        <video controls {poster_attr} style="max-width: 100%; border-radius: 8px; background: #000;">
                            <source src="{local_video}" type="video/mp4">
                            您的浏览器不支持视频播放
                        </video>
                    </div>
                    ''')
                    continue
            
            # 没有本地文件，显示占位符
            if video['type'] == 'wechat_native':
                cover_url = video.get('cover_url', '')
                cover_html = f'<img src="{cover_url}" style="max-width: 100%; border-radius: 8px;" alt="视频封面">' if cover_url else ''
                html_parts.append(f'''
                <div class="video-placeholder" style="background: #f0f0f0; padding: 20px; text-align: center; margin: 15px 0; border-radius: 8px;">
                    {cover_html}
                    <p style="margin-top: 10px;">🎬 视频 {i} (微信视频)</p>
                    <p style="color: #999; font-size: 12px;">视频ID: {vid}</p>
                    <p><a href="{video.get('data_src', '#')}" target="_blank" style="color: #576b95;">点击观看原视频</a></p>
                </div>
                ''')
            elif video['type'] == 'iframe':
                html_parts.append(f'''
                <div class="video-placeholder" style="background: #f0f0f0; padding: 20px; text-align: center; margin: 15px 0; border-radius: 8px;">
                    <p>🎬 视频 {i}</p>
                    <p><a href="{video.get('url', '#')}" target="_blank" style="color: #576b95;">点击观看原视频</a></p>
                </div>
                ''')
            elif video['type'] == 'tencent':
                html_parts.append(f'''
                <div class="video-placeholder" style="background: #f0f0f0; padding: 20px; text-align: center; margin: 15px 0; border-radius: 8px;">
                    <p>🎬 腾讯视频 {i}</p>
                    <p style="color: #999; font-size: 12px;">视频ID: {video.get('video_id', '未知')}</p>
                </div>
                ''')
            else:
                html_parts.append(f'''
                <div class="video-placeholder" style="background: #f0f0f0; padding: 20px; text-align: center; margin: 15px 0; border-radius: 8px;">
                    <p>🎬 视频 {i}</p>
                    <p><a href="{video.get('url', '#')}" target="_blank" style="color: #576b95;">点击观看</a></p>
                </div>
                ''')
        
        html_parts.append('</div>')
        return '\n'.join(html_parts)
    
    def _save_article(self, article: dict, html_content: str) -> str:
        """保存文章到文件"""
        # 生成安全的文件名
        title = article['title']
        
        # 移除不安全字符 (增强版：替换掉所有标点符号和空格，只保留中文、字母、数字、下划线、连字符)
        # \w 包含 [a-zA-Z0-9_]，\u4e00-\u9fa5 是常用汉字范围
        safe_title = re.sub(r'[^\w\u4e00-\u9fa5\-]', '_', title)
        # 合并连续的下划线
        safe_title = re.sub(r'_+', '_', safe_title)
        # 去除首尾下划线
        safe_title = safe_title.strip('_')
        
        if not safe_title:
            safe_title = "article"
            
        safe_title = safe_title[:50]  # 限制长度
        
        timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
        # 确保文件名是 UTF-8 编码安全的（虽然 Python 3 默认是 unicode，但在某些文件系统上可能需要注意）
        filename = f"{safe_title}_{timestamp}.html"
        filepath = os.path.join(self.output_dir, filename)
        
        # 打印调试信息
        print(f"  💾 保存文件: {filepath}")
        
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
