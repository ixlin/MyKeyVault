using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MyKeyVault.Web.Data;
using MyKeyVault.Web.Models;

namespace MyKeyVault.Web.Services;

/// <summary>
/// HTML 演示文稿生成服务
/// </summary>
public class HtmlPresentationService
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<HtmlPresentationService> _logger;
    private readonly IMemoryCache _cache;

    // 设计令牌 - 科技蓝简约风格
    private static readonly ThemeConfig DefaultTheme = new()
    {
        Name = "科技蓝",
        Colors = new ThemeColors
        {
            Background = "#0a0a0f",
            Surface = "#12121a",
            SurfaceLight = "#1a1a2e",
            Text = "#FFFFFF",
            TextSecondary = "#94a3b8",
            Accent = "#3b82f6",
            AccentLight = "#60a5fa",
            AccentGlow = "rgba(59, 130, 246, 0.3)",
            Border = "#2d2d3a",
            Success = "#10b981",
            Warning = "#f59e0b"
        },
        Typography = new ThemeTypography
        {
            FontFamily = "'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif",
            TitleSize = "56px",
            H2Size = "36px",
            BodySize = "22px",
            SmallSize = "16px"
        }
    };

    public HtmlPresentationService(
        ApplicationDbContext context,
        IWebHostEnvironment env,
        ILogger<HtmlPresentationService> logger,
        IMemoryCache cache)
    {
        _context = context;
        _env = env;
        _logger = logger;
        _cache = cache;
    }

    /// <summary>
    /// 生成 HTML 演示文稿（异步后台任务）
    /// </summary>
    public async Task GenerateAsync(long extractionId, string userId, string progressKey)
    {
        try
        {
            UpdateProgress(progressKey, 0, "开始生成演示文稿...");

            // 1. 读取萃取结果
            UpdateProgress(progressKey, 5, "读取萃取结果...");
            var extraction = await _context.WechatArticleExtractions
                .FirstOrDefaultAsync(e => e.ExtractionId == extractionId && e.UserId == userId);

            if (extraction == null)
            {
                throw new Exception("萃取记录不存在");
            }

            var article = await _context.WechatArticles
                .FirstOrDefaultAsync(a => a.ArticleId == extraction.ArticleId);

            if (article == null)
            {
                throw new Exception("文章不存在");
            }

            // 2. 解析 Markdown 为 Slides
            UpdateProgress(progressKey, 15, "解析文档结构...");
            var slides = ParseMarkdownToSlides(extraction.Result ?? "", article.Title ?? "未命名演示文稿");

            if (slides.Count == 0)
            {
                throw new Exception("无法解析出有效的幻灯片内容");
            }

            // 3. 创建输出目录
            UpdateProgress(progressKey, 30, "创建文件结构...");
            var presentationId = $"{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}".Substring(0, 24);
            var outputDir = Path.Combine(_env.WebRootPath, "html-ppt", userId, presentationId);
            var slidesDir = Path.Combine(outputDir, "slides");
            
            Directory.CreateDirectory(slidesDir);

            // 4. 生成每一页幻灯片
            UpdateProgress(progressKey, 40, "生成幻灯片页面...");
            for (int i = 0; i < slides.Count; i++)
            {
                var progress = 40 + (int)((double)(i + 1) / slides.Count * 40);
                UpdateProgress(progressKey, progress, $"生成第 {i + 1}/{slides.Count} 页...");

                var slideHtml = GenerateSlideHtml(slides[i], i + 1, slides.Count);
                await File.WriteAllTextAsync(
                    Path.Combine(slidesDir, $"slide{i + 1}.html"),
                    slideHtml,
                    Encoding.UTF8);
            }

            // 5. 生成 index.html (播放器)
            UpdateProgress(progressKey, 85, "生成播放器页面...");
            var indexHtml = GenerateIndexHtml(slides, article.Title ?? "演示文稿");
            await File.WriteAllTextAsync(
                Path.Combine(outputDir, "index.html"),
                indexHtml,
                Encoding.UTF8);

            // 6. 保存元数据
            UpdateProgress(progressKey, 95, "保存元数据...");
            var metadata = new PresentationMetadata
            {
                Title = article.Title ?? "演示文稿",
                Author = article.Author,
                CreatedAt = DateTime.UtcNow,
                SlideCount = slides.Count,
                Theme = DefaultTheme.Name,
                SourceArticleId = article.ArticleId,
                ExtractionId = extractionId
            };
            await File.WriteAllTextAsync(
                Path.Combine(outputDir, "presentation.json"),
                JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }),
                Encoding.UTF8);

            // 7. 完成
            var viewUrl = $"/html-ppt/{userId}/{presentationId}/index.html";
            UpdateProgress(progressKey, 100, "生成完成！", "completed", null, viewUrl, presentationId);

            _logger.LogInformation("HTML 演示文稿生成成功: {ViewUrl}", viewUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成 HTML 演示文稿失败");
            UpdateProgress(progressKey, 0, "生成失败", "failed", ex.Message);
        }
    }

    /// <summary>
    /// 解析 Markdown 为幻灯片数据
    /// </summary>
    private List<SlideModel> ParseMarkdownToSlides(string markdown, string articleTitle)
    {
        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        var document = Markdown.Parse(markdown, pipeline);
        var slides = new List<SlideModel>();
        SlideModel? currentSlide = null;

        // 添加封面页
        slides.Add(new SlideModel
        {
            Type = SlideType.Title,
            Title = articleTitle,
            Subtitle = $"生成时间：{DateTime.Now:yyyy年MM月dd日}"
        });

        foreach (var block in document)
        {
            if (block is HeadingBlock heading && heading.Level == 2)
            {
                // H2 标题 = 新幻灯片
                if (currentSlide != null && (currentSlide.Bullets.Any() || currentSlide.ChartData != null))
                {
                    slides.Add(currentSlide);
                }

                currentSlide = new SlideModel
                {
                    Type = SlideType.Content,
                    Title = GetInlineText(heading.Inline)
                };
            }
            else if (block is ListBlock list && currentSlide != null)
            {
                foreach (ListItemBlock item in list)
                {
                    var text = GetBlockText(item);
                    if (!string.IsNullOrEmpty(text))
                    {
                        currentSlide.Bullets.Add(text);
                    }
                }
            }
            else if (block is FencedCodeBlock code)
            {
                var info = code.Info?.Trim().ToLower() ?? "";
                var codeContent = code.Lines.ToString();

                if (info == "mermaid")
                {
                    // Mermaid 图表
                    if (currentSlide != null)
                    {
                        currentSlide.MermaidCode = codeContent;
                        currentSlide.Type = SlideType.Diagram;
                    }
                }
                else if (info == "chart-data" || info == "chart" || info == "echarts")
                {
                    // ECharts 图表数据
                    try
                    {
                        var chartData = JsonSerializer.Deserialize<ChartDataModel>(codeContent);
                        if (chartData != null && currentSlide != null)
                        {
                            currentSlide.ChartData = chartData;
                            currentSlide.Type = SlideType.Data;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "解析图表数据失败: {Content}", codeContent);
                    }
                }
            }
            else if (block is ParagraphBlock paragraph && currentSlide != null)
            {
                var text = GetInlineText(paragraph.Inline);
                // 检查是否包含数字统计（如 "40%"、"1000万" 等）
                if (!string.IsNullOrEmpty(text) && Regex.IsMatch(text, @"\d+[%万亿+]|\d{3,}"))
                {
                    // 可能是关键数据
                    if (currentSlide.KeyMetrics.Count < 3)
                    {
                        currentSlide.KeyMetrics.Add(text);
                    }
                    else
                    {
                        currentSlide.Bullets.Add(text);
                    }
                }
                else if (!string.IsNullOrEmpty(text) && text.Length < 200)
                {
                    currentSlide.Bullets.Add(text);
                }
            }
        }

        // 保存最后一个幻灯片
        if (currentSlide != null && (currentSlide.Bullets.Any() || currentSlide.ChartData != null || !string.IsNullOrEmpty(currentSlide.MermaidCode)))
        {
            slides.Add(currentSlide);
        }

        // 添加结尾页
        slides.Add(new SlideModel
        {
            Type = SlideType.End,
            Title = "感谢阅读",
            Subtitle = "由 AI 智能萃取生成"
        });

        return slides;
    }

    /// <summary>
    /// 生成单个幻灯片 HTML
    /// </summary>
    private string GenerateSlideHtml(SlideModel slide, int slideNumber, int totalSlides)
    {
        var theme = DefaultTheme;
        
        return slide.Type switch
        {
            SlideType.Title => GenerateTitleSlideHtml(slide, theme),
            SlideType.End => GenerateEndSlideHtml(slide, theme),
            SlideType.Data => GenerateDataSlideHtml(slide, theme, slideNumber, totalSlides),
            SlideType.Diagram => GenerateDiagramSlideHtml(slide, theme, slideNumber, totalSlides),
            _ => GenerateContentSlideHtml(slide, theme, slideNumber, totalSlides)
        };
    }

    /// <summary>
    /// 生成封面页 HTML
    /// </summary>
    private string GenerateTitleSlideHtml(SlideModel slide, ThemeConfig theme)
    {
        return $@"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=1280, height=720, initial-scale=1.0"">
    <title>{EscapeHtml(slide.Title)}</title>
    <link href=""https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700&display=swap"" rel=""stylesheet"">
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{
            width: 1280px;
            height: 720px;
            background: {theme.Colors.Background};
            font-family: {theme.Typography.FontFamily};
            color: {theme.Colors.Text};
            overflow: hidden;
            display: flex;
            justify-content: center;
            align-items: center;
        }}
        .slide-container {{
            width: 100%;
            height: 100%;
            position: relative;
            background: linear-gradient(135deg, {theme.Colors.Background} 0%, {theme.Colors.SurfaceLight} 50%, {theme.Colors.Background} 100%);
            display: flex;
            flex-direction: column;
            justify-content: center;
            align-items: center;
            text-align: center;
            padding: 60px;
        }}
        .glow-orb {{
            position: absolute;
            width: 400px;
            height: 400px;
            background: radial-gradient(circle, {theme.Colors.AccentGlow} 0%, transparent 70%);
            border-radius: 50%;
            filter: blur(60px);
            top: 50%;
            left: 50%;
            transform: translate(-50%, -50%);
            z-index: 0;
        }}
        .content {{
            position: relative;
            z-index: 1;
        }}
        .tag {{
            background: {theme.Colors.AccentGlow};
            border: 1px solid {theme.Colors.Accent};
            color: {theme.Colors.AccentLight};
            padding: 8px 20px;
            border-radius: 20px;
            font-size: 14px;
            font-weight: 600;
            text-transform: uppercase;
            letter-spacing: 2px;
            margin-bottom: 30px;
            display: inline-block;
        }}
        h1 {{
            font-size: {theme.Typography.TitleSize};
            font-weight: 700;
            line-height: 1.2;
            margin-bottom: 24px;
            max-width: 900px;
            background: linear-gradient(90deg, {theme.Colors.Text} 0%, {theme.Colors.AccentLight} 100%);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            background-clip: text;
        }}
        .subtitle {{
            font-size: 20px;
            color: {theme.Colors.TextSecondary};
            margin-top: 20px;
        }}
        .decoration {{
            position: absolute;
            bottom: 40px;
            left: 50%;
            transform: translateX(-50%);
            display: flex;
            gap: 8px;
        }}
        .dot {{
            width: 8px;
            height: 8px;
            background: {theme.Colors.Accent};
            border-radius: 50%;
            opacity: 0.6;
        }}
        .dot:nth-child(2) {{ opacity: 0.8; }}
        .dot:nth-child(3) {{ opacity: 1; }}
    </style>
</head>
<body>
    <div class=""slide-container"">
        <div class=""glow-orb""></div>
        <div class=""content"">
            <div class=""tag"">AI 智能萃取</div>
            <h1>{EscapeHtml(slide.Title)}</h1>
            <p class=""subtitle"">{EscapeHtml(slide.Subtitle ?? "")}</p>
        </div>
        <div class=""decoration"">
            <div class=""dot""></div>
            <div class=""dot""></div>
            <div class=""dot""></div>
        </div>
    </div>
</body>
</html>";
    }

    /// <summary>
    /// 生成内容页 HTML
    /// </summary>
    private string GenerateContentSlideHtml(SlideModel slide, ThemeConfig theme, int slideNumber, int totalSlides)
    {
        var bulletsHtml = new StringBuilder();
        foreach (var bullet in slide.Bullets.Take(6))
        {
            bulletsHtml.AppendLine($@"<li><span class=""bullet-icon"">▸</span>{EscapeHtml(bullet)}</li>");
        }

        var metricsHtml = "";
        if (slide.KeyMetrics.Any())
        {
            var metricsBuilder = new StringBuilder();
            metricsBuilder.AppendLine(@"<div class=""metrics-row"">");
            foreach (var metric in slide.KeyMetrics.Take(3))
            {
                // 提取数字部分
                var match = Regex.Match(metric, @"(\d+[%万亿+]?)");
                var number = match.Success ? match.Groups[1].Value : "";
                var description = metric.Replace(number, "").Trim();
                
                metricsBuilder.AppendLine($@"
                <div class=""metric-card"">
                    <div class=""metric-value"">{EscapeHtml(number)}</div>
                    <div class=""metric-label"">{EscapeHtml(description)}</div>
                </div>");
            }
            metricsBuilder.AppendLine("</div>");
            metricsHtml = metricsBuilder.ToString();
        }

        return $@"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=1280, height=720, initial-scale=1.0"">
    <title>{EscapeHtml(slide.Title)}</title>
    <link href=""https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700&display=swap"" rel=""stylesheet"">
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{
            width: 1280px;
            height: 720px;
            background: {theme.Colors.Background};
            font-family: {theme.Typography.FontFamily};
            color: {theme.Colors.Text};
            overflow: hidden;
        }}
        .slide-container {{
            width: 100%;
            height: 100%;
            padding: 50px 60px;
            display: flex;
            flex-direction: column;
            background: linear-gradient(180deg, {theme.Colors.Background} 0%, {theme.Colors.SurfaceLight} 100%);
        }}
        header {{
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 40px;
            padding-bottom: 20px;
            border-bottom: 1px solid {theme.Colors.Border};
        }}
        h2 {{
            font-size: {theme.Typography.H2Size};
            font-weight: 700;
            display: flex;
            align-items: center;
            gap: 16px;
        }}
        .slide-number {{
            font-size: 14px;
            color: {theme.Colors.TextSecondary};
            background: {theme.Colors.Surface};
            padding: 6px 12px;
            border-radius: 6px;
        }}
        .main-content {{
            flex: 1;
            display: flex;
            flex-direction: column;
            gap: 30px;
        }}
        .metrics-row {{
            display: flex;
            gap: 24px;
            margin-bottom: 20px;
        }}
        .metric-card {{
            background: {theme.Colors.Surface};
            border: 1px solid {theme.Colors.Border};
            border-radius: 12px;
            padding: 24px 32px;
            flex: 1;
            text-align: center;
        }}
        .metric-value {{
            font-size: 42px;
            font-weight: 700;
            color: {theme.Colors.Accent};
            margin-bottom: 8px;
        }}
        .metric-label {{
            font-size: 16px;
            color: {theme.Colors.TextSecondary};
        }}
        .bullets {{
            list-style: none;
            display: flex;
            flex-direction: column;
            gap: 20px;
        }}
        .bullets li {{
            font-size: {theme.Typography.BodySize};
            line-height: 1.6;
            display: flex;
            align-items: flex-start;
            gap: 16px;
            padding: 16px 20px;
            background: {theme.Colors.Surface};
            border-radius: 8px;
            border-left: 3px solid {theme.Colors.Accent};
        }}
        .bullet-icon {{
            color: {theme.Colors.Accent};
            font-size: 18px;
            flex-shrink: 0;
            margin-top: 2px;
        }}
    </style>
</head>
<body>
    <div class=""slide-container"">
        <header>
            <h2>{EscapeHtml(slide.Title)}</h2>
            <span class=""slide-number"">{slideNumber} / {totalSlides}</span>
        </header>
        <div class=""main-content"">
            {metricsHtml}
            <ul class=""bullets"">
                {bulletsHtml}
            </ul>
        </div>
    </div>
</body>
</html>";
    }

    /// <summary>
    /// 生成数据图表页 HTML
    /// </summary>
    private string GenerateDataSlideHtml(SlideModel slide, ThemeConfig theme, int slideNumber, int totalSlides)
    {
        var chartConfig = GenerateEChartsConfig(slide.ChartData!, theme);

        return $@"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=1280, height=720, initial-scale=1.0"">
    <title>{EscapeHtml(slide.Title)}</title>
    <link href=""https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700&display=swap"" rel=""stylesheet"">
    <script src=""https://cdn.jsdelivr.net/npm/echarts@5.4.3/dist/echarts.min.js""></script>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{
            width: 1280px;
            height: 720px;
            background: {theme.Colors.Background};
            font-family: {theme.Typography.FontFamily};
            color: {theme.Colors.Text};
            overflow: hidden;
        }}
        .slide-container {{
            width: 100%;
            height: 100%;
            padding: 50px 60px;
            display: flex;
            flex-direction: column;
            background: linear-gradient(180deg, {theme.Colors.Background} 0%, {theme.Colors.SurfaceLight} 100%);
        }}
        header {{
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 30px;
            padding-bottom: 20px;
            border-bottom: 1px solid {theme.Colors.Border};
        }}
        h2 {{
            font-size: {theme.Typography.H2Size};
            font-weight: 700;
        }}
        .slide-number {{
            font-size: 14px;
            color: {theme.Colors.TextSecondary};
            background: {theme.Colors.Surface};
            padding: 6px 12px;
            border-radius: 6px;
        }}
        .chart-container {{
            flex: 1;
            background: {theme.Colors.Surface};
            border: 1px solid {theme.Colors.Border};
            border-radius: 12px;
            padding: 20px;
        }}
        #chart {{
            width: 100%;
            height: 100%;
        }}
    </style>
</head>
<body>
    <div class=""slide-container"">
        <header>
            <h2>{EscapeHtml(slide.Title)}</h2>
            <span class=""slide-number"">{slideNumber} / {totalSlides}</span>
        </header>
        <div class=""chart-container"">
            <div id=""chart""></div>
        </div>
    </div>
    <script>
        const chart = echarts.init(document.getElementById('chart'));
        const option = {chartConfig};
        chart.setOption(option);
        window.addEventListener('resize', () => chart.resize());
    </script>
</body>
</html>";
    }

    /// <summary>
    /// 生成 Mermaid 图表页 HTML
    /// </summary>
    private string GenerateDiagramSlideHtml(SlideModel slide, ThemeConfig theme, int slideNumber, int totalSlides)
    {
        var mermaidCode = slide.MermaidCode?.Replace("`", "\\`").Replace("$", "\\$") ?? "";

        return $@"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=1280, height=720, initial-scale=1.0"">
    <title>{EscapeHtml(slide.Title)}</title>
    <link href=""https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700&display=swap"" rel=""stylesheet"">
    <script src=""https://cdn.jsdelivr.net/npm/mermaid@10.6.1/dist/mermaid.min.js""></script>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{
            width: 1280px;
            height: 720px;
            background: {theme.Colors.Background};
            font-family: {theme.Typography.FontFamily};
            color: {theme.Colors.Text};
            overflow: hidden;
        }}
        .slide-container {{
            width: 100%;
            height: 100%;
            padding: 50px 60px;
            display: flex;
            flex-direction: column;
            background: linear-gradient(180deg, {theme.Colors.Background} 0%, {theme.Colors.SurfaceLight} 100%);
        }}
        header {{
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 30px;
            padding-bottom: 20px;
            border-bottom: 1px solid {theme.Colors.Border};
        }}
        h2 {{
            font-size: {theme.Typography.H2Size};
            font-weight: 700;
        }}
        .slide-number {{
            font-size: 14px;
            color: {theme.Colors.TextSecondary};
            background: {theme.Colors.Surface};
            padding: 6px 12px;
            border-radius: 6px;
        }}
        .diagram-container {{
            flex: 1;
            background: {theme.Colors.Surface};
            border: 1px solid {theme.Colors.Border};
            border-radius: 12px;
            padding: 30px;
            display: flex;
            justify-content: center;
            align-items: center;
            overflow: auto;
        }}
        .mermaid {{
            max-width: 100%;
        }}
        .mermaid svg {{
            max-width: 100%;
            height: auto;
        }}
    </style>
</head>
<body>
    <div class=""slide-container"">
        <header>
            <h2>{EscapeHtml(slide.Title)}</h2>
            <span class=""slide-number"">{slideNumber} / {totalSlides}</span>
        </header>
        <div class=""diagram-container"">
            <pre class=""mermaid"">{EscapeHtml(slide.MermaidCode ?? "")}</pre>
        </div>
    </div>
    <script>
        mermaid.initialize({{
            startOnLoad: true,
            theme: 'dark',
            themeVariables: {{
                primaryColor: '{theme.Colors.Accent}',
                primaryTextColor: '{theme.Colors.Text}',
                primaryBorderColor: '{theme.Colors.Border}',
                lineColor: '{theme.Colors.AccentLight}',
                secondaryColor: '{theme.Colors.Surface}',
                tertiaryColor: '{theme.Colors.SurfaceLight}'
            }}
        }});
    </script>
</body>
</html>";
    }

    /// <summary>
    /// 生成结尾页 HTML
    /// </summary>
    private string GenerateEndSlideHtml(SlideModel slide, ThemeConfig theme)
    {
        return $@"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=1280, height=720, initial-scale=1.0"">
    <title>{EscapeHtml(slide.Title)}</title>
    <link href=""https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700&display=swap"" rel=""stylesheet"">
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{
            width: 1280px;
            height: 720px;
            background: {theme.Colors.Background};
            font-family: {theme.Typography.FontFamily};
            color: {theme.Colors.Text};
            overflow: hidden;
            display: flex;
            justify-content: center;
            align-items: center;
        }}
        .slide-container {{
            width: 100%;
            height: 100%;
            position: relative;
            background: radial-gradient(ellipse at center, {theme.Colors.SurfaceLight} 0%, {theme.Colors.Background} 70%);
            display: flex;
            flex-direction: column;
            justify-content: center;
            align-items: center;
            text-align: center;
        }}
        .glow-orb {{
            position: absolute;
            width: 300px;
            height: 300px;
            background: radial-gradient(circle, {theme.Colors.AccentGlow} 0%, transparent 70%);
            border-radius: 50%;
            filter: blur(40px);
            top: 50%;
            left: 50%;
            transform: translate(-50%, -50%);
        }}
        .content {{
            position: relative;
            z-index: 1;
        }}
        h1 {{
            font-size: 64px;
            font-weight: 700;
            margin-bottom: 20px;
            color: {theme.Colors.Text};
        }}
        .subtitle {{
            font-size: 20px;
            color: {theme.Colors.TextSecondary};
        }}
        .footer {{
            position: absolute;
            bottom: 40px;
            font-size: 14px;
            color: {theme.Colors.TextSecondary};
            opacity: 0.6;
        }}
    </style>
</head>
<body>
    <div class=""slide-container"">
        <div class=""glow-orb""></div>
        <div class=""content"">
            <h1>{EscapeHtml(slide.Title)}</h1>
            <p class=""subtitle"">{EscapeHtml(slide.Subtitle ?? "")}</p>
        </div>
        <div class=""footer"">MyKeyVault · AI 文章萃取 · {DateTime.Now:yyyy}</div>
    </div>
</body>
</html>";
    }

    /// <summary>
    /// 生成 ECharts 配置
    /// </summary>
    private string GenerateEChartsConfig(ChartDataModel chartData, ThemeConfig theme)
    {
        var chartType = chartData.Type?.ToLower() ?? "bar";
        
        var config = new
        {
            backgroundColor = "transparent",
            title = new
            {
                text = chartData.Title ?? "",
                textStyle = new { color = theme.Colors.Text, fontSize = 20 },
                left = "center"
            },
            tooltip = new { trigger = chartType == "pie" ? "item" : "axis" },
            legend = new
            {
                textStyle = new { color = theme.Colors.TextSecondary },
                bottom = 10
            },
            grid = chartType != "pie" ? new { left = "3%", right = "4%", bottom = "15%", containLabel = true } : null,
            xAxis = chartType != "pie" ? new
            {
                type = "category",
                data = chartData.XAxis ?? Array.Empty<string>(),
                axisLabel = new { color = theme.Colors.TextSecondary },
                axisLine = new { lineStyle = new { color = theme.Colors.Border } }
            } : null,
            yAxis = chartType != "pie" ? new
            {
                type = "value",
                axisLabel = new { color = theme.Colors.TextSecondary },
                splitLine = new { lineStyle = new { color = theme.Colors.Border, opacity = 0.3 } }
            } : null,
            series = chartData.Series?.Select((s, i) => new
            {
                name = s.Name ?? $"系列{i + 1}",
                type = chartType,
                data = s.Data ?? Array.Empty<object>(),
                itemStyle = new { color = i == 0 ? theme.Colors.Accent : (i == 1 ? theme.Colors.Success : theme.Colors.Warning) },
                radius = chartType == "pie" ? "60%" : null,
                center = chartType == "pie" ? new[] { "50%", "50%" } : null
            }).ToArray() ?? Array.Empty<object>()
        };

        return JsonSerializer.Serialize(config, new JsonSerializerOptions 
        { 
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    }

    /// <summary>
    /// 生成 index.html (播放器)
    /// </summary>
    private string GenerateIndexHtml(List<SlideModel> slides, string title)
    {
        var slidesArray = string.Join(", ", Enumerable.Range(1, slides.Count).Select(i => $"\"slides/slide{i}.html\""));
        var theme = DefaultTheme;

        return $@"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{EscapeHtml(title)} - 演示文稿</title>
    <link href=""https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700&display=swap"" rel=""stylesheet"">
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{
            background: {theme.Colors.Background};
            font-family: {theme.Typography.FontFamily};
            color: {theme.Colors.Text};
            min-height: 100vh;
        }}
        .container {{
            max-width: 1600px;
            margin: 0 auto;
            padding: 20px;
        }}
        header {{
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 16px 24px;
            background: {theme.Colors.Surface};
            border-radius: 12px;
            margin-bottom: 24px;
        }}
        header h1 {{
            font-size: 18px;
            font-weight: 600;
            color: {theme.Colors.Text};
        }}
        .controls {{
            display: flex;
            gap: 12px;
        }}
        .btn {{
            background: {theme.Colors.Accent};
            color: white;
            border: none;
            padding: 10px 20px;
            border-radius: 8px;
            cursor: pointer;
            font-size: 14px;
            font-weight: 500;
            display: flex;
            align-items: center;
            gap: 8px;
            transition: all 0.2s;
        }}
        .btn:hover {{
            background: {theme.Colors.AccentLight};
            transform: translateY(-1px);
        }}
        .btn-secondary {{
            background: {theme.Colors.Surface};
            border: 1px solid {theme.Colors.Border};
        }}
        .btn-secondary:hover {{
            background: {theme.Colors.SurfaceLight};
        }}
        .preview-container {{
            display: flex;
            flex-direction: column;
            gap: 24px;
            align-items: center;
        }}
        .slide-preview {{
            width: 90vw;
            max-width: 1280px;
            aspect-ratio: 16/9;
            background: {theme.Colors.Surface};
            border-radius: 12px;
            overflow: hidden;
            box-shadow: 0 4px 20px rgba(0,0,0,0.3);
            cursor: pointer;
            transition: all 0.2s;
            position: relative;
        }}
        .slide-preview:hover {{
            transform: translateY(-4px);
            box-shadow: 0 8px 30px rgba(59, 130, 246, 0.2);
        }}
        .slide-number {{
            position: absolute;
            top: 12px;
            left: 12px;
            background: rgba(0,0,0,0.7);
            color: white;
            padding: 6px 12px;
            border-radius: 6px;
            font-size: 12px;
            font-weight: 600;
            z-index: 10;
        }}
        .slide-frame-container {{
            width: 100%;
            height: 100%;
            position: relative;
            overflow: hidden;
        }}
        .slide-frame {{
            position: absolute;
            top: 50%;
            left: 50%;
            width: 1280px;
            height: 720px;
            border: none;
            transform-origin: center center;
        }}
        /* 演示模式 */
        .presentation-mode {{
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            background: black;
            z-index: 1000;
            display: none;
            flex-direction: column;
        }}
        .presentation-slide-container {{
            flex: 1;
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 20px;
        }}
        .presentation-slide {{
            width: 1280px;
            height: 720px;
            border: none;
            transform-origin: center center;
        }}
        .presentation-controls {{
            height: 60px;
            background: rgba(0,0,0,0.9);
            display: flex;
            justify-content: center;
            align-items: center;
            gap: 20px;
            padding: 0 20px;
        }}
        .presentation-controls button {{
            background: transparent;
            color: white;
            border: none;
            width: 40px;
            height: 40px;
            border-radius: 50%;
            cursor: pointer;
            display: flex;
            align-items: center;
            justify-content: center;
            transition: background 0.2s;
        }}
        .presentation-controls button:hover {{
            background: rgba(255,255,255,0.1);
        }}
        .progress-bar {{
            flex: 1;
            max-width: 400px;
            height: 4px;
            background: rgba(255,255,255,0.2);
            border-radius: 2px;
            overflow: hidden;
        }}
        .progress-fill {{
            height: 100%;
            background: {theme.Colors.Accent};
            transition: width 0.3s;
        }}
        .slide-counter {{
            color: white;
            font-size: 14px;
            min-width: 60px;
            text-align: center;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <header>
            <h1>📊 {EscapeHtml(title)}</h1>
            <div class=""controls"">
                <button class=""btn"" onclick=""startPresentation(0)"">
                    <svg width=""16"" height=""16"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2""><polygon points=""5 3 19 12 5 21 5 3""/></svg>
                    开始演示
                </button>
            </div>
        </header>
        <div class=""preview-container"" id=""preview-container""></div>
    </div>

    <div class=""presentation-mode"" id=""presentation-mode"">
        <div class=""presentation-slide-container"">
            <iframe id=""presentation-slide"" class=""presentation-slide""></iframe>
        </div>
        <div class=""presentation-controls"">
            <button onclick=""prevSlide()"" title=""上一页"">
                <svg width=""20"" height=""20"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2""><polyline points=""15 18 9 12 15 6""/></svg>
            </button>
            <div class=""progress-bar"">
                <div class=""progress-fill"" id=""progress-fill""></div>
            </div>
            <span class=""slide-counter""><span id=""current-slide"">1</span> / <span id=""total-slides"">{slides.Count}</span></span>
            <button onclick=""nextSlide()"" title=""下一页"">
                <svg width=""20"" height=""20"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2""><polyline points=""9 18 15 12 9 6""/></svg>
            </button>
            <button onclick=""toggleFullscreen()"" title=""全屏"">
                <svg width=""20"" height=""20"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2""><polyline points=""15 3 21 3 21 9""/><polyline points=""9 21 3 21 3 15""/><line x1=""21"" y1=""3"" x2=""14"" y2=""10""/><line x1=""3"" y1=""21"" x2=""10"" y2=""14""/></svg>
            </button>
            <button onclick=""exitPresentation()"" title=""退出"">
                <svg width=""20"" height=""20"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2""><line x1=""18"" y1=""6"" x2=""6"" y2=""18""/><line x1=""6"" y1=""6"" x2=""18"" y2=""18""/></svg>
            </button>
        </div>
    </div>

    <script>
        const slides = [{slidesArray}];
        let currentIndex = 0;

        // 生成预览
        function generatePreviews() {{
            const container = document.getElementById('preview-container');
            slides.forEach((slide, index) => {{
                const preview = document.createElement('div');
                preview.className = 'slide-preview';
                preview.innerHTML = `
                    <div class=""slide-number"">${{index + 1}}</div>
                    <div class=""slide-frame-container"">
                        <iframe class=""slide-frame"" src=""${{slide}}"" frameborder=""0""></iframe>
                    </div>
                `;
                preview.onclick = () => startPresentation(index);
                container.appendChild(preview);
                
                // 计算缩放
                setTimeout(() => {{
                    const frameContainer = preview.querySelector('.slide-frame-container');
                    const frame = preview.querySelector('.slide-frame');
                    const scale = Math.min(
                        frameContainer.clientWidth / 1280,
                        frameContainer.clientHeight / 720
                    );
                    frame.style.transform = `translate(-50%, -50%) scale(${{scale}})`;
                }}, 100);
            }});
        }}

        // 开始演示
        function startPresentation(index) {{
            currentIndex = index;
            document.getElementById('presentation-mode').style.display = 'flex';
            document.body.style.overflow = 'hidden';
            updateSlide();
            requestFullscreen();
        }}

        // 更新幻灯片
        function updateSlide() {{
            document.getElementById('presentation-slide').src = slides[currentIndex];
            document.getElementById('current-slide').textContent = currentIndex + 1;
            document.getElementById('progress-fill').style.width = ((currentIndex + 1) / slides.length * 100) + '%';
            scalePresentationSlide();
        }}

        // 缩放演示幻灯片
        function scalePresentationSlide() {{
            const container = document.querySelector('.presentation-slide-container');
            const slide = document.getElementById('presentation-slide');
            const scale = Math.min(
                (container.clientWidth - 40) / 1280,
                (container.clientHeight - 40) / 720
            );
            slide.style.transform = `scale(${{scale}})`;
        }}

        function nextSlide() {{
            if (currentIndex < slides.length - 1) {{
                currentIndex++;
                updateSlide();
            }}
        }}

        function prevSlide() {{
            if (currentIndex > 0) {{
                currentIndex--;
                updateSlide();
            }}
        }}

        function exitPresentation() {{
            if (document.fullscreenElement) {{
                document.exitFullscreen();
            }}
            document.getElementById('presentation-mode').style.display = 'none';
            document.body.style.overflow = 'auto';
        }}

        function requestFullscreen() {{
            const elem = document.getElementById('presentation-mode');
            if (elem.requestFullscreen) elem.requestFullscreen();
            else if (elem.webkitRequestFullscreen) elem.webkitRequestFullscreen();
        }}

        function toggleFullscreen() {{
            if (document.fullscreenElement) {{
                document.exitFullscreen();
            }} else {{
                requestFullscreen();
            }}
        }}

        // 键盘控制
        document.addEventListener('keydown', (e) => {{
            if (document.getElementById('presentation-mode').style.display === 'flex') {{
                if (e.key === 'ArrowRight' || e.key === ' ' || e.key === 'Enter') {{
                    e.preventDefault();
                    nextSlide();
                }} else if (e.key === 'ArrowLeft') {{
                    e.preventDefault();
                    prevSlide();
                }} else if (e.key === 'Escape') {{
                    exitPresentation();
                }} else if (e.key === 'f' || e.key === 'F') {{
                    toggleFullscreen();
                }}
            }}
        }});

        // 监听窗口大小变化
        window.addEventListener('resize', () => {{
            if (document.getElementById('presentation-mode').style.display === 'flex') {{
                scalePresentationSlide();
            }}
        }});

        // 初始化
        window.addEventListener('load', generatePreviews);
    </script>
</body>
</html>";
    }

    /// <summary>
    /// 更新进度
    /// </summary>
    private void UpdateProgress(string key, int percentage, string message, string status = "processing", string? error = null, string? viewUrl = null, string? presentationId = null)
    {
        var progress = new HtmlPresentationProgress
        {
            Percentage = percentage,
            Message = message,
            Status = status,
            Error = error,
            ViewUrl = viewUrl,
            PresentationId = presentationId
        };

        _cache.Set(key, progress, TimeSpan.FromMinutes(10));
        _logger.LogInformation("HTML 演示文稿生成进度: {Percentage}% - {Message}", percentage, message);
    }

    /// <summary>
    /// 获取进度
    /// </summary>
    public HtmlPresentationProgress? GetProgress(string key)
    {
        return _cache.Get<HtmlPresentationProgress>(key);
    }

    /// <summary>
    /// HTML 转义
    /// </summary>
    private static string EscapeHtml(string text)
    {
        return System.Net.WebUtility.HtmlEncode(text ?? "");
    }

    /// <summary>
    /// 提取行内文本
    /// </summary>
    private string GetInlineText(ContainerInline? inline)
    {
        if (inline == null) return "";

        var sb = new StringBuilder();
        foreach (var item in inline)
        {
            if (item is LiteralInline literal)
            {
                sb.Append(literal.Content);
            }
            else if (item is CodeInline code)
            {
                sb.Append(code.Content);
            }
            else if (item is EmphasisInline emphasis)
            {
                sb.Append(GetInlineText(emphasis));
            }
        }
        return sb.ToString().Trim();
    }

    /// <summary>
    /// 提取块级文本
    /// </summary>
    private string GetBlockText(Block block)
    {
        var sb = new StringBuilder();
        if (block is LeafBlock leaf && leaf.Inline != null)
        {
            sb.Append(GetInlineText(leaf.Inline));
        }
        else if (block is ContainerBlock container)
        {
            foreach (var child in container)
            {
                sb.Append(GetBlockText(child));
                sb.Append(" ");
            }
        }
        return sb.ToString().Trim();
    }
}

#region 模型定义

/// <summary>
/// 幻灯片类型
/// </summary>
public enum SlideType
{
    Title,
    Content,
    Data,
    Diagram,
    End
}

/// <summary>
/// 幻灯片模型
/// </summary>
public class SlideModel
{
    public SlideType Type { get; set; } = SlideType.Content;
    public string Title { get; set; } = "";
    public string? Subtitle { get; set; }
    public List<string> Bullets { get; set; } = new();
    public List<string> KeyMetrics { get; set; } = new();
    public ChartDataModel? ChartData { get; set; }
    public string? MermaidCode { get; set; }
}

/// <summary>
/// 图表数据模型
/// </summary>
public class ChartDataModel
{
    public string? Type { get; set; }
    public string? Title { get; set; }
    public string[]? XAxis { get; set; }
    public ChartSeriesModel[]? Series { get; set; }
}

/// <summary>
/// 图表系列模型
/// </summary>
public class ChartSeriesModel
{
    public string? Name { get; set; }
    public object[]? Data { get; set; }
}

/// <summary>
/// 主题配置
/// </summary>
public class ThemeConfig
{
    public string Name { get; set; } = "默认";
    public ThemeColors Colors { get; set; } = new();
    public ThemeTypography Typography { get; set; } = new();
}

/// <summary>
/// 主题颜色
/// </summary>
public class ThemeColors
{
    public string Background { get; set; } = "#121212";
    public string Surface { get; set; } = "#1E1E1E";
    public string SurfaceLight { get; set; } = "#2C2C2C";
    public string Text { get; set; } = "#FFFFFF";
    public string TextSecondary { get; set; } = "#A0A0A0";
    public string Accent { get; set; } = "#3B82F6";
    public string AccentLight { get; set; } = "#60A5FA";
    public string AccentGlow { get; set; } = "rgba(59, 130, 246, 0.3)";
    public string Border { get; set; } = "#333333";
    public string Success { get; set; } = "#10B981";
    public string Warning { get; set; } = "#F59E0B";
}

/// <summary>
/// 主题排版
/// </summary>
public class ThemeTypography
{
    public string FontFamily { get; set; } = "'Inter', sans-serif";
    public string TitleSize { get; set; } = "56px";
    public string H2Size { get; set; } = "36px";
    public string BodySize { get; set; } = "22px";
    public string SmallSize { get; set; } = "16px";
}

/// <summary>
/// 演示文稿元数据
/// </summary>
public class PresentationMetadata
{
    public string Title { get; set; } = "";
    public string? Author { get; set; }
    public DateTime CreatedAt { get; set; }
    public int SlideCount { get; set; }
    public string Theme { get; set; } = "默认";
    public long SourceArticleId { get; set; }
    public long ExtractionId { get; set; }
}

/// <summary>
/// 生成进度
/// </summary>
public class HtmlPresentationProgress
{
    public int Percentage { get; set; }
    public string Message { get; set; } = "";
    public string Status { get; set; } = "processing";
    public string? Error { get; set; }
    public string? ViewUrl { get; set; }
    public string? PresentationId { get; set; }
}

#endregion
