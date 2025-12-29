#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
微信公众号文章爬取 HTTP 服务
基于 FastAPI 提供 REST API
"""

import os
import re
import asyncio
import shutil
import traceback
import logging
from datetime import datetime, timedelta
from typing import List, Optional, Dict, Any
from urllib.parse import urlparse, parse_qs
from contextlib import asynccontextmanager

from fastapi import FastAPI, HTTPException, BackgroundTasks
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, Field
import uvicorn

from scraper import WechatArticleScraper

# 配置日志
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

# 单个文章爬取超时时间（秒）
ARTICLE_SCRAPE_TIMEOUT = 300  # 5分钟
# 任务过期时间（小时）
TASK_EXPIRE_HOURS = 24
# 孤立任务检查间隔（秒）
ORPHAN_CHECK_INTERVAL = 300  # 5分钟

# 后台清理任务的控制标志
cleanup_task: Optional[asyncio.Task] = None

async def cleanup_orphaned_tasks():
    """后台任务：定期清理孤立/超时的任务"""
    while True:
        try:
            await asyncio.sleep(ORPHAN_CHECK_INTERVAL)
            now = datetime.now()
            expired_task_ids = []
            
            for task_id, task in list(tasks.items()):
                try:
                    # 检查任务是否过期（超过24小时）
                    created_at = datetime.fromisoformat(task.get('created_at', now.isoformat()))
                    if (now - created_at).total_seconds() > TASK_EXPIRE_HOURS * 3600:
                        expired_task_ids.append(task_id)
                        continue
                    
                    # 检查是否有任务长时间处于 processing 状态（超过10分钟）
                    updated_at = datetime.fromisoformat(task.get('updated_at', now.isoformat()))
                    if task.get('status') == 'processing' and (now - updated_at).total_seconds() > 600:
                        # 标记为超时失败
                        for article in task.get('articles', []):
                            if article.status == 'processing':
                                article.status = 'failed'
                                article.error_message = '任务处理超时（超过10分钟无响应）'
                        task['status'] = 'failed'
                        task['updated_at'] = now.isoformat()
                        logger.warning(f"任务 {task_id} 因超时被标记为失败")
                except Exception as e:
                    logger.error(f"检查任务 {task_id} 时出错: {e}")
            
            # 清理过期任务
            for task_id in expired_task_ids:
                try:
                    # 先停止可能正在运行的爬虫
                    if task_id in running_scrapers:
                        for article_id, scraper in list(running_scrapers[task_id].items()):
                            try:
                                scraper.stop()
                            except Exception:
                                pass
                        del running_scrapers[task_id]
                    del tasks[task_id]
                    logger.info(f"已清理过期任务: {task_id}")
                except Exception as e:
                    logger.error(f"清理任务 {task_id} 时出错: {e}")
                    
        except asyncio.CancelledError:
            logger.info("清理任务已取消")
            break
        except Exception as e:
            logger.error(f"清理任务执行出错: {e}")

@asynccontextmanager
async def lifespan(app: FastAPI):
    """应用生命周期管理"""
    global cleanup_task
    # 启动时：启动后台清理任务
    cleanup_task = asyncio.create_task(cleanup_orphaned_tasks())
    logger.info("微信图文爬取服务已启动")
    yield
    # 关闭时：停止后台任务
    if cleanup_task:
        cleanup_task.cancel()
        try:
            await cleanup_task
        except asyncio.CancelledError:
            pass
    logger.info("微信图文爬取服务已关闭")

app = FastAPI(
    title="微信图文爬取服务",
    description="提供微信公众号文章内容抓取的 REST API",
    version="1.2.0",
    lifespan=lifespan
)

# CORS 配置（仅允许本地访问）
app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:5000", "http://127.0.0.1:5000", "https://mykeyvault.cn"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# 任务存储（内存中，生产环境可以改用 Redis）
tasks = {}
# 保存正在运行的 Scraper 实例引用，用于强制停止
running_scrapers = {}  # {task_id: {article_id: WechatArticleScraper}}


class ScrapeRequest(BaseModel):
    """爬取请求"""
    urls: List[str] = Field(..., description="微信公众号文章链接列表", max_length=10)
    output_base: str = Field(..., description="输出基础目录")
    user_id: str = Field(..., description="用户ID，用于隔离文件")


class ArticleResult(BaseModel):
    """单篇文章结果"""
    article_id: str
    source_url: str
    title: Optional[str] = None
    author: Optional[str] = None
    publish_time: Optional[str] = None
    html_file_path: Optional[str] = None
    pdf_file_path: Optional[str] = None
    images_count: int = 0
    videos_count: int = 0
    progress: int = 0  # 0-100
    stage: str = "等待开始"  # 面向用户的阶段提示
    status: str = "pending"  # pending, processing, completed, failed, cancelled
    error_message: Optional[str] = None


class ScrapeResponse(BaseModel):
    """爬取响应"""
    task_id: str
    status: str  # pending, processing, completed, failed
    articles: List[ArticleResult] = []
    completed_count: int = 0
    total_count: int = 0


class TaskStatusResponse(BaseModel):
    """任务状态响应"""
    task_id: str
    status: str
    articles: List[ArticleResult] = []
    completed_count: int = 0
    total_count: int = 0
    created_at: str
    updated_at: str


class CancelResponse(BaseModel):
    """取消任务响应"""
    success: bool
    message: str
    stopped_articles: List[str] = []
    deleted_dirs: List[str] = []


def extract_article_id(url: str) -> str:
    """
    从微信链接中提取文章ID
    
    支持格式：
    - https://mp.weixin.qq.com/s/x_gisj2FZdorl07_M4Loww
    - https://mp.weixin.qq.com/s?__biz=xxx&mid=xxx&idx=xxx&sn=xxx
    """
    parsed = urlparse(url)
    path = parsed.path
    
    # 格式1: /s/xxxxx
    if '/s/' in path:
        article_id = path.split('/s/')[-1].strip('/')
        if article_id:
            return article_id
    
    # 格式2: 带参数的链接
    query_params = parse_qs(parsed.query)
    if 'sn' in query_params:
        return query_params['sn'][0]
    
    # 使用 URL hash 作为后备
    import hashlib
    return hashlib.md5(url.encode()).hexdigest()[:16]


def generate_task_id() -> str:
    """生成任务ID"""
    import uuid
    return f"task_{datetime.now().strftime('%Y%m%d%H%M%S')}_{uuid.uuid4().hex[:8]}"


async def scrape_article_with_timeout(scraper: WechatArticleScraper, url: str, timeout: int = ARTICLE_SCRAPE_TIMEOUT):
    """带超时控制的爬取执行"""
    try:
        # 在后台线程中执行同步 Playwright，并添加超时控制
        result = await asyncio.wait_for(
            asyncio.to_thread(scraper.scrape, url),
            timeout=timeout
        )
        return result
    except asyncio.TimeoutError:
        # 超时时尝试停止爬虫
        try:
            scraper.stop()
        except Exception:
            pass
        raise Exception(f"爬取超时（超过 {timeout} 秒）")


async def scrape_article(article_result: ArticleResult, output_dir: str, task_id: str = ""):
    """异步爬取单篇文章（在独立线程中运行同步 Playwright，避免事件循环冲突）"""
    scraper = None
    start_time = datetime.now()
    
    try:
        article_result.status = "processing"
        article_result.progress = 0
        article_result.stage = "准备中"
        logger.info(f"开始爬取文章: {article_result.source_url}")

        def on_progress(percent: int, message: str):
            # 只写面向用户的阶段信息，不暴露内部实现细节
            article_result.progress = int(percent)
            article_result.stage = message
            # 更新任务的 updated_at 时间，防止被误判为超时
            if task_id and task_id in tasks:
                tasks[task_id]['updated_at'] = datetime.now().isoformat()
        
        scraper = WechatArticleScraper(output_dir=output_dir, progress_callback=on_progress)
        
        # 保存 scraper 引用，以便外部可以强制停止
        if task_id:
            if task_id not in running_scrapers:
                running_scrapers[task_id] = {}
            running_scrapers[task_id][article_result.article_id] = scraper
        
        # 使用带超时控制的爬取
        result = await scrape_article_with_timeout(scraper, article_result.source_url)
        
        article_result.title = result.get("title")
        article_result.author = result.get("author")
        article_result.publish_time = result.get("publish_time")
        article_result.html_file_path = result.get("output_file")
        article_result.pdf_file_path = result.get("pdf_file")
        article_result.images_count = result.get("images_count", 0)
        article_result.videos_count = result.get("videos_count", 0)
        article_result.status = "completed"
        article_result.progress = 100
        article_result.stage = "完成"
        
        elapsed = (datetime.now() - start_time).total_seconds()
        logger.info(f"文章爬取成功: {article_result.title} (耗时 {elapsed:.1f}s)")
        
    except asyncio.CancelledError:
        article_result.status = "cancelled"
        article_result.error_message = "任务已被取消"
        article_result.stage = "已取消"
        logger.warning(f"文章爬取被取消: {article_result.source_url}")
        # 尝试停止爬虫
        if scraper:
            try:
                scraper.stop()
            except Exception:
                pass
    except Exception as e:
        article_result.status = "failed"
        # 尽量让前端看到"卡在哪一步"
        if article_result.progress < 100:
            article_result.stage = article_result.stage or "失败"
        # 将错误信息缩短并明确，便于前端展示与调试
        msg = str(e)
        if len(msg) > 500:
            msg = msg[:500] + "..."
        article_result.error_message = msg
        
        elapsed = (datetime.now() - start_time).total_seconds()
        logger.error(f"文章爬取失败: {article_result.source_url} (耗时 {elapsed:.1f}s): {msg}")
        logger.debug(traceback.format_exc())
    finally:
        # 清理 scraper 引用
        if task_id and task_id in running_scrapers:
            if article_result.article_id in running_scrapers[task_id]:
                del running_scrapers[task_id][article_result.article_id]
            if not running_scrapers[task_id]:
                del running_scrapers[task_id]


async def process_scrape_task(task_id: str, request: ScrapeRequest):
    """处理爬取任务"""
    task = tasks.get(task_id)
    if not task:
        return
    
    task["status"] = "processing"
    task["updated_at"] = datetime.now().isoformat()
    
    for article in task["articles"]:
        # 检查任务是否已被取消
        if task.get("cancelled"):
            if article.status == "pending":
                article.status = "cancelled"
                article.error_message = "任务已被用户取消"
            continue
            
        # 创建文章专属目录: output_base/user_id/article_id/
        article_dir = os.path.join(
            request.output_base, 
            request.user_id,
            article.article_id
        )
        os.makedirs(article_dir, exist_ok=True)
        
        await scrape_article(article, article_dir, task_id)
        
        task["completed_count"] += 1
        task["updated_at"] = datetime.now().isoformat()
    
    # 检查最终状态
    failed_count = sum(1 for a in task["articles"] if a.status == "failed")
    cancelled_count = sum(1 for a in task["articles"] if a.status == "cancelled")
    if task.get("cancelled"):
        task["status"] = "cancelled"
    elif failed_count == len(task["articles"]):
        task["status"] = "failed"
    elif failed_count > 0 or cancelled_count > 0:
        task["status"] = "partial"
    else:
        task["status"] = "completed"
    
    task["updated_at"] = datetime.now().isoformat()


@app.get("/health")
async def health_check():
    """健康检查"""
    return {"status": "ok", "service": "wechat-scraper", "time": datetime.now().isoformat()}


@app.post("/api/scrape", response_model=ScrapeResponse)
async def scrape_articles(request: ScrapeRequest, background_tasks: BackgroundTasks):
    """
    提交爬取任务
    
    - 最多支持 10 个链接
    - 返回任务ID，可通过 /api/task/{task_id} 查询状态
    """
    # 验证链接数量
    if len(request.urls) > 10:
        raise HTTPException(status_code=400, detail="最多支持 10 个链接")
    
    if not request.urls:
        raise HTTPException(status_code=400, detail="至少需要 1 个链接")
    
    # 验证链接格式
    for url in request.urls:
        if "mp.weixin.qq.com" not in url:
            raise HTTPException(status_code=400, detail=f"无效的微信链接: {url}")
    
    # 生成任务ID
    task_id = generate_task_id()
    
    # 初始化文章结果
    articles = []
    for url in request.urls:
        article_id = extract_article_id(url)
        articles.append(ArticleResult(
            article_id=article_id,
            source_url=url,
            status="pending"
        ))
    
    # 创建任务
    task = {
        "task_id": task_id,
        "status": "pending",
        "articles": articles,
        "completed_count": 0,
        "total_count": len(articles),
        "created_at": datetime.now().isoformat(),
        "updated_at": datetime.now().isoformat(),
        "request": request,
        "cancelled": False
    }
    tasks[task_id] = task
    
    # 后台执行爬取任务
    background_tasks.add_task(process_scrape_task, task_id, request)
    
    return ScrapeResponse(
        task_id=task_id,
        status="pending",
        articles=articles,
        completed_count=0,
        total_count=len(articles)
    )


@app.get("/api/task/{task_id}", response_model=TaskStatusResponse)
async def get_task_status(task_id: str):
    """查询任务状态"""
    task = tasks.get(task_id)
    if not task:
        raise HTTPException(status_code=404, detail="任务不存在")
    
    return TaskStatusResponse(
        task_id=task["task_id"],
        status=task["status"],
        articles=task["articles"],
        completed_count=task["completed_count"],
        total_count=task["total_count"],
        created_at=task["created_at"],
        updated_at=task["updated_at"]
    )


@app.post("/api/task/{task_id}/cancel", response_model=CancelResponse)
async def cancel_task(task_id: str, delete_files: bool = True):
    """
    取消/终止任务
    
    - 设置取消标志，阻止后续文章处理
    - 强制停止正在运行的爬虫进程
    - 可选删除已下载的文件
    """
    task = tasks.get(task_id)
    if not task:
        raise HTTPException(status_code=404, detail="任务不存在")
    
    stopped_articles = []
    deleted_dirs = []
    
    # 设置取消标志
    task["cancelled"] = True
    task["status"] = "cancelling"
    task["updated_at"] = datetime.now().isoformat()
    
    # 强制停止正在运行的爬虫
    if task_id in running_scrapers:
        for article_id, scraper in list(running_scrapers[task_id].items()):
            try:
                scraper.stop()
                stopped_articles.append(article_id)
            except Exception as e:
                print(f"停止爬虫失败 {article_id}: {e}")
    
    # 更新文章状态
    for article in task["articles"]:
        if article.status in ["pending", "processing"]:
            article.status = "cancelled"
            article.error_message = "任务已被用户取消"
    
    # 删除已下载的文件
    if delete_files:
        request = task.get("request")
        if request:
            for article in task["articles"]:
                article_dir = os.path.join(
                    request.output_base,
                    request.user_id,
                    article.article_id
                )
                if os.path.exists(article_dir):
                    try:
                        shutil.rmtree(article_dir)
                        deleted_dirs.append(article_dir)
                    except Exception as e:
                        print(f"删除目录失败 {article_dir}: {e}")
    
    task["status"] = "cancelled"
    task["updated_at"] = datetime.now().isoformat()
    
    return CancelResponse(
        success=True,
        message="任务已取消",
        stopped_articles=stopped_articles,
        deleted_dirs=deleted_dirs
    )


@app.delete("/api/task/{task_id}")
async def delete_task(task_id: str):
    """删除任务记录（仅从内存中移除，不删除文件）"""
    if task_id not in tasks:
        raise HTTPException(status_code=404, detail="任务不存在")
    
    # 先尝试停止正在运行的爬虫
    if task_id in running_scrapers:
        for article_id, scraper in list(running_scrapers[task_id].items()):
            try:
                scraper.stop()
            except Exception:
                pass
    
    del tasks[task_id]
    return {"message": "任务已删除", "task_id": task_id}


@app.delete("/api/task/{task_id}/article/{article_id}")
async def cancel_article(task_id: str, article_id: str, delete_files: bool = True):
    """
    取消单篇文章的抓取
    
    - 强制停止正在运行的爬虫
    - 可选删除已下载的文件
    """
    task = tasks.get(task_id)
    if not task:
        raise HTTPException(status_code=404, detail="任务不存在")
    
    # 查找文章
    article = None
    for a in task["articles"]:
        if a.article_id == article_id:
            article = a
            break
    
    if not article:
        raise HTTPException(status_code=404, detail="文章不存在")
    
    deleted_dir = None
    
    # 强制停止爬虫
    if task_id in running_scrapers and article_id in running_scrapers[task_id]:
        try:
            running_scrapers[task_id][article_id].stop()
        except Exception as e:
            print(f"停止爬虫失败 {article_id}: {e}")
    
    # 更新状态
    article.status = "cancelled"
    article.error_message = "已被用户取消"
    
    # 删除文件
    if delete_files:
        request = task.get("request")
        if request:
            article_dir = os.path.join(
                request.output_base,
                request.user_id,
                article_id
            )
            if os.path.exists(article_dir):
                try:
                    shutil.rmtree(article_dir)
                    deleted_dir = article_dir
                except Exception as e:
                    print(f"删除目录失败 {article_dir}: {e}")
    
    task["updated_at"] = datetime.now().isoformat()
    
    return {
        "success": True,
        "message": "文章抓取已取消",
        "article_id": article_id,
        "deleted_dir": deleted_dir
    }


@app.get("/api/tasks")
async def list_tasks(limit: int = 20):
    """列出最近的任务"""
    sorted_tasks = sorted(
        tasks.values(), 
        key=lambda x: x["created_at"], 
        reverse=True
    )[:limit]
    
    return [
        {
            "task_id": t["task_id"],
            "status": t["status"],
            "total_count": t["total_count"],
            "completed_count": t["completed_count"],
            "created_at": t["created_at"],
            "updated_at": t["updated_at"]
        }
        for t in sorted_tasks
    ]


if __name__ == "__main__":
    uvicorn.run(
        "app:app",
        host="127.0.0.1",
        port=5001,
        reload=False,
        log_level="info"
    )
