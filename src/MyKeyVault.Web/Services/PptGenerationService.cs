using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MyKeyVault.Web.Data;
using MyKeyVault.Web.Models;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace MyKeyVault.Web.Services;

/// <summary>
/// PPT 生成服务
/// </summary>
public class PptGenerationService
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<PptGenerationService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;

    public PptGenerationService(
        ApplicationDbContext context,
        IWebHostEnvironment env,
        ILogger<PptGenerationService> logger,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache)
    {
        _context = context;
        _env = env;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _cache = cache;
    }

    /// <summary>
    /// 生成 PPT（异步后台任务）
    /// </summary>
    public async Task GeneratePptAsync(long extractionId, string userId, string progressKey)
    {
        try
        {
            UpdateProgress(progressKey, 0, "开始生成 PPT...");

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

            // 2. 解析 Markdown
            UpdateProgress(progressKey, 15, "解析 Markdown 结构...");
            var slides = await ParseMarkdownToSlides(extraction.Result ?? "", progressKey);

            // 3. 生成 PPT 文件
            UpdateProgress(progressKey, 80, "创建 PPT 文档...");
            var fileName = SanitizeFileName($"{article.Title}_PPT_{DateTime.Now:yyyyMMddHHmmss}.pptx");
            var tempPath = Path.Combine(Path.GetTempPath(), fileName);

            await CreatePowerPointDocumentAsync(tempPath, slides, article.Title ?? "未命名");

            UpdateProgress(progressKey, 95, "保存文件...");

            // 4. 完成
            UpdateProgress(progressKey, 100, "生成完成！", "completed", null, $"/WechatArticle/DownloadPpt?file={Uri.EscapeDataString(fileName)}", fileName);

            _logger.LogInformation("PPT 生成成功: {FileName}", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成 PPT 失败");
            UpdateProgress(progressKey, 0, "生成失败", "failed", ex.Message);
        }
    }

    /// <summary>
    /// 解析 Markdown 为幻灯片数据
    /// </summary>
    private async Task<List<SlideData>> ParseMarkdownToSlides(string markdown, string progressKey)
    {
        UpdateProgress(progressKey, 20, "分析文档结构...");

        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        var document = Markdown.Parse(markdown, pipeline);
        var slides = new List<SlideData>();
        SlideData? currentSlide = null;

        UpdateProgress(progressKey, 30, "提取页面内容...");

        foreach (var block in document)
        {
            if (block is HeadingBlock heading && heading.Level == 2)
            {
                // 保存前一个幻灯片
                if (currentSlide != null)
                {
                    slides.Add(currentSlide);
                }

                // 创建新幻灯片
                currentSlide = new SlideData
                {
                    Title = GetInlineText(heading.Inline)
                };
            }
            else if (block is ListBlock list && currentSlide != null)
            {
                // 提取列表项
                foreach (ListItemBlock item in list)
                {
                    var text = GetBlockText(item);
                    if (!string.IsNullOrEmpty(text))
                    {
                        currentSlide.Bullets.Add(text);
                    }
                }
            }
            else if (block is FencedCodeBlock code && code.Info?.Trim().ToLower() == "mermaid")
            {
                // Mermaid 图表
                var mermaidCode = code.Lines.ToString();
                if (currentSlide != null)
                {
                    currentSlide.MermaidCode = mermaidCode;
                }
                else
                {
                    // 如果没有当前幻灯片，创建一个专门放图表的
                    slides.Add(new SlideData
                    {
                        Title = "图表",
                        MermaidCode = mermaidCode
                    });
                }
            }
            else if (block is ParagraphBlock paragraph && currentSlide != null)
            {
                var text = GetInlineText(paragraph.Inline);
                if (!string.IsNullOrEmpty(text) && text.Length < 200)
                {
                    currentSlide.Bullets.Add(text);
                }
            }
        }

        // 保存最后一个幻灯片
        if (currentSlide != null)
        {
            slides.Add(currentSlide);
        }

        // 转换 Mermaid 图表
        var mermaidSlides = slides.Where(s => !string.IsNullOrEmpty(s.MermaidCode)).ToList();
        if (mermaidSlides.Any())
        {
            for (int i = 0; i < mermaidSlides.Count; i++)
            {
                var progress = 45 + (int)((double)(i + 1) / mermaidSlides.Count * 30);
                UpdateProgress(progressKey, progress, $"转换 Mermaid 图表（第 {i + 1}/{mermaidSlides.Count} 张）...");

                try
                {
                    mermaidSlides[i].MermaidSvg = await ConvertMermaidToSvg(mermaidSlides[i].MermaidCode!);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Mermaid 转换失败，跳过该图表");
                    mermaidSlides[i].MermaidCode = null; // 转换失败则不显示
                }
            }
        }

        return slides;
    }

    /// <summary>
    /// 转换 Mermaid 代码为 SVG
    /// </summary>
    private async Task<byte[]> ConvertMermaidToSvg(string mermaidCode)
    {
        // 增加重试机制
        int maxRetries = 3;
        Exception? lastException = null;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                // 使用 mermaid.ink API
                var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(mermaidCode));
                var url = $"https://mermaid.ink/svg/{encoded}";

                _logger.LogInformation("转换 Mermaid (尝试 {Retry}/{MaxRetries}): {Url}", i + 1, maxRetries, url);

                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var svgData = await response.Content.ReadAsByteArrayAsync();
                    _logger.LogInformation("Mermaid 转换成功，SVG 大小: {Size} bytes", svgData.Length);
                    return svgData;
                }

                _logger.LogWarning("Mermaid API 返回错误状态码: {StatusCode}", response.StatusCode);
                lastException = new HttpRequestException($"HTTP {response.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Mermaid 转换失败 (尝试 {Retry}/{MaxRetries})", i + 1, maxRetries);
                lastException = ex;
            }

            // 如果不是最后一次尝试，等待后重试
            if (i < maxRetries - 1)
            {
                await Task.Delay(1000 * (i + 1)); // 递增延迟：1s, 2s, 3s
            }
        }

        _logger.LogError(lastException, "Mermaid 转换最终失败，已重试 {MaxRetries} 次", maxRetries);
        throw lastException ?? new Exception("Mermaid 转换失败");
    }

    /// <summary>
    /// 创建 PowerPoint 文档
    /// </summary>
    private async Task CreatePowerPointDocumentAsync(string filePath, List<SlideData> slides, string title)
    {
        using var presentationDocument = PresentationDocument.Create(filePath, PresentationDocumentType.Presentation);

        var presentationPart = presentationDocument.AddPresentationPart();
        presentationPart.Presentation = new Presentation();

        var slideMasterIdList = new SlideMasterIdList(new SlideMasterId { Id = 2147483648U, RelationshipId = "rId1" });
        var slideIdList = new SlideIdList();
        var slideSize = new SlideSize { Cx = 9144000, Cy = 6858000, Type = SlideSizeValues.Screen4x3 };
        var notesSize = new NotesSize { Cx = 6858000, Cy = 9144000 };
        var defaultTextStyle = new DefaultTextStyle();

        presentationPart.Presentation.Append(slideMasterIdList, slideIdList, slideSize, notesSize, defaultTextStyle);

        // 创建默认的 SlideMaster
        CreateDefaultSlideMaster(presentationPart);

        uint slideId = 256;

        // 添加封面页
        AddTitleSlide(presentationPart, slideIdList, title, ref slideId);

        // 添加内容页
        foreach (var slideData in slides)
        {
            if (slideData.MermaidSvg != null && slideData.MermaidSvg.Length > 0)
            {
                // 图表页
                slideId = await AddDiagramSlide(presentationPart, slideIdList, slideData, slideId);
            }
            else if (slideData.Bullets.Any())
            {
                // 内容页
                AddContentSlide(presentationPart, slideIdList, slideData, ref slideId);
            }
        }

        presentationPart.Presentation.Save();
    }

    /// <summary>
    /// 创建默认 SlideMaster
    /// </summary>
    private void CreateDefaultSlideMaster(PresentationPart presentationPart)
    {
        var slideMasterPart = presentationPart.AddNewPart<SlideMasterPart>("rId1");
        
        slideMasterPart.SlideMaster = new SlideMaster(
            new P.CommonSlideData(new P.ShapeTree(
                new P.NonVisualGroupShapeProperties(
                    new P.NonVisualDrawingProperties { Id = 1U, Name = "" },
                    new P.NonVisualGroupShapeDrawingProperties(),
                    new ApplicationNonVisualDrawingProperties()),
                new GroupShapeProperties(new A.TransformGroup()))),
            new P.ColorMap
            {
                Background1 = A.ColorSchemeIndexValues.Light1,
                Text1 = A.ColorSchemeIndexValues.Dark1,
                Background2 = A.ColorSchemeIndexValues.Light2,
                Text2 = A.ColorSchemeIndexValues.Dark2,
                Accent1 = A.ColorSchemeIndexValues.Accent1,
                Accent2 = A.ColorSchemeIndexValues.Accent2,
                Accent3 = A.ColorSchemeIndexValues.Accent3,
                Accent4 = A.ColorSchemeIndexValues.Accent4,
                Accent5 = A.ColorSchemeIndexValues.Accent5,
                Accent6 = A.ColorSchemeIndexValues.Accent6,
                Hyperlink = A.ColorSchemeIndexValues.Hyperlink,
                FollowedHyperlink = A.ColorSchemeIndexValues.FollowedHyperlink
            },
            new P.SlideLayoutIdList(new P.SlideLayoutId { Id = 2147483649U, RelationshipId = "rId1" }),
            new P.TextStyles(
                new P.TitleStyle(),
                new P.BodyStyle(),
                new P.OtherStyle()));

        // 添加主题部分
        var themePart = slideMasterPart.AddNewPart<ThemePart>("rId2");
        themePart.Theme = new A.Theme(
            new A.ThemeElements(
                new A.ColorScheme(
                    new A.Dark1Color(new A.SystemColor { Val = A.SystemColorValues.WindowText, LastColor = "000000" }),
                    new A.Light1Color(new A.SystemColor { Val = A.SystemColorValues.Window, LastColor = "FFFFFF" }),
                    new A.Dark2Color(new A.RgbColorModelHex { Val = "44546A" }),
                    new A.Light2Color(new A.RgbColorModelHex { Val = "E7E6E6" }),
                    new A.Accent1Color(new A.RgbColorModelHex { Val = "4472C4" }),
                    new A.Accent2Color(new A.RgbColorModelHex { Val = "ED7D31" }),
                    new A.Accent3Color(new A.RgbColorModelHex { Val = "A5A5A5" }),
                    new A.Accent4Color(new A.RgbColorModelHex { Val = "FFC000" }),
                    new A.Accent5Color(new A.RgbColorModelHex { Val = "5B9BD5" }),
                    new A.Accent6Color(new A.RgbColorModelHex { Val = "70AD47" }),
                    new A.Hyperlink(new A.RgbColorModelHex { Val = "0563C1" }),
                    new A.FollowedHyperlinkColor(new A.RgbColorModelHex { Val = "954F72" }))
                { Name = "Office" },
                new A.FontScheme(
                    new A.MajorFont(
                        new A.LatinFont { Typeface = "Calibri Light", Panose = "020F0302020204030204" },
                        new A.EastAsianFont { Typeface = "" },
                        new A.ComplexScriptFont { Typeface = "" }),
                    new A.MinorFont(
                        new A.LatinFont { Typeface = "Calibri", Panose = "020F0502020204030204" },
                        new A.EastAsianFont { Typeface = "" },
                        new A.ComplexScriptFont { Typeface = "" }))
                { Name = "Office" },
                new A.FormatScheme(
                    new A.FillStyleList(),
                    new A.LineStyleList(),
                    new A.EffectStyleList(),
                    new A.BackgroundFillStyleList())
                { Name = "Office" }))
        { Name = "Office Theme" };

        var slideLayoutPart = slideMasterPart.AddNewPart<SlideLayoutPart>("rId1");
        slideLayoutPart.SlideLayout = new SlideLayout(
            new P.CommonSlideData(new P.ShapeTree(
                new P.NonVisualGroupShapeProperties(
                    new P.NonVisualDrawingProperties { Id = 1U, Name = "" },
                    new P.NonVisualGroupShapeDrawingProperties(),
                    new ApplicationNonVisualDrawingProperties()),
                new GroupShapeProperties(new A.TransformGroup()))),
            new P.ColorMapOverride(new A.MasterColorMapping()))
        { Type = SlideLayoutValues.Blank };
    }

    /// <summary>
    /// 添加封面页
    /// </summary>
    private void AddTitleSlide(PresentationPart presentationPart, SlideIdList slideIdList, string title, ref uint slideId)
    {
        var slidePart = presentationPart.AddNewPart<SlidePart>();
        slideIdList.Append(new SlideId { Id = slideId++, RelationshipId = presentationPart.GetIdOfPart(slidePart) });

        var slide = new Slide(
            new P.CommonSlideData(
                new P.ShapeTree(
                    new P.NonVisualGroupShapeProperties(
                        new P.NonVisualDrawingProperties { Id = 1U, Name = "" },
                        new P.NonVisualGroupShapeDrawingProperties(),
                        new ApplicationNonVisualDrawingProperties()),
                    new GroupShapeProperties(),
                    // 标题
                    CreateTextShape(2U, title, 1524000, 1524000, 6858000, 1200000, 4400, true)
                )));

        slidePart.Slide = slide;
    }

    /// <summary>
    /// 添加内容页
    /// </summary>
    private void AddContentSlide(PresentationPart presentationPart, SlideIdList slideIdList, SlideData slideData, ref uint slideId)
    {
        var slidePart = presentationPart.AddNewPart<SlidePart>();
        slideIdList.Append(new SlideId { Id = slideId++, RelationshipId = presentationPart.GetIdOfPart(slidePart) });

        var shapes = new List<OpenXmlElement>
        {
            new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties { Id = 1U, Name = "" },
                new P.NonVisualGroupShapeDrawingProperties(),
                new ApplicationNonVisualDrawingProperties()),
            new GroupShapeProperties(),
            // 标题
            CreateTextShape(2U, slideData.Title, 457200, 274638, 8229600, 1143000, 3200, true)
        };

        // 要点内容
        if (slideData.Bullets.Any())
        {
            var bulletText = string.Join("\n", slideData.Bullets);
            shapes.Add(CreateTextShape(3U, bulletText, 457200, 1600200, 8229600, 4525963, 2000, false));
        }

        var slide = new Slide(new P.CommonSlideData(new P.ShapeTree(shapes.ToArray())));
        slidePart.Slide = slide;
    }

    /// <summary>
    /// 添加图表页
    /// </summary>
    private async Task<uint> AddDiagramSlide(PresentationPart presentationPart, SlideIdList slideIdList, SlideData slideData, uint slideId)
    {
        var slidePart = presentationPart.AddNewPart<SlidePart>();
        slideIdList.Append(new SlideId { Id = slideId, RelationshipId = presentationPart.GetIdOfPart(slidePart) });

        var shapes = new List<OpenXmlElement>
        {
            new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties { Id = 1U, Name = "" },
                new P.NonVisualGroupShapeDrawingProperties(),
                new ApplicationNonVisualDrawingProperties()),
            new GroupShapeProperties(),
            // 标题
            CreateTextShape(2U, slideData.Title, 457200, 274638, 8229600, 1143000, 3200, true)
        };

        // 嵌入图片（注：Mermaid.ink 返回 SVG，这里临时保存为文件再嵌入）
        if (slideData.MermaidSvg != null && slideData.MermaidSvg.Length > 0)
        {
            try
            {
                // 将 SVG 保存为临时文件
                var tempSvgPath = Path.Combine(Path.GetTempPath(), $"mermaid_{Guid.NewGuid():N}.svg");
                await File.WriteAllBytesAsync(tempSvgPath, slideData.MermaidSvg);

                var imagePart = slidePart.AddImagePart(ImagePartType.Svg);
                using (var stream = new FileStream(tempSvgPath, FileMode.Open, FileAccess.Read))
                {
                    imagePart.FeedData(stream);
                }

                // 删除临时文件
                try { File.Delete(tempSvgPath); } catch { }

                var imageShape = CreateImageShape(3U, presentationPart, slidePart, imagePart, 1524000, 2286000, 6096000, 3810000);
                shapes.Add(imageShape);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SVG 嵌入失败，使用文本占位");
                shapes.Add(CreateTextShape(3U, "[Mermaid 图表]", 1524000, 2286000, 6096000, 3810000, 1800, false));
            }
        }

        var slide = new Slide(new P.CommonSlideData(new P.ShapeTree(shapes.ToArray())));
        slidePart.Slide = slide;

        return slideId + 1;
    }

    /// <summary>
    /// 创建文本形状
    /// </summary>
    private P.Shape CreateTextShape(uint id, string text, long x, long y, long width, long height, int fontSize, bool isBold)
    {
        return new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = $"TextBox {id}" },
                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                new ApplicationNonVisualDrawingProperties(new PlaceholderShape())),
            new P.ShapeProperties(
                new A.Transform2D(
                    new A.Offset { X = x, Y = y },
                    new A.Extents { Cx = width, Cy = height })),
            new P.TextBody(
                new A.BodyProperties(),
                new A.ListStyle(),
                new A.Paragraph(
                    new A.ParagraphProperties { Alignment = A.TextAlignmentTypeValues.Left },
                    new A.Run(
                        new A.RunProperties
                        {
                            Language = "zh-CN",
                            FontSize = fontSize,
                            Bold = isBold,
                            Dirty = false
                        },
                        new A.Text { Text = text }))));
    }

    /// <summary>
    /// 创建图片形状
    /// </summary>
    private P.Picture CreateImageShape(uint id, PresentationPart presentationPart, SlidePart slidePart, ImagePart imagePart, long x, long y, long width, long height)
    {
        var relationshipId = slidePart.GetIdOfPart(imagePart);

        return new P.Picture(
            new P.NonVisualPictureProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = $"Picture {id}" },
                new P.NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true }),
                new ApplicationNonVisualDrawingProperties()),
            new P.BlipFill(
                new A.Blip { Embed = relationshipId },
                new A.Stretch(new A.FillRectangle())),
            new P.ShapeProperties(
                new A.Transform2D(
                    new A.Offset { X = x, Y = y },
                    new A.Extents { Cx = width, Cy = height }),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }));
    }

    /// <summary>
    /// 更新进度
    /// </summary>
    private void UpdateProgress(string key, int percentage, string message, string status = "processing", string? error = null, string? downloadUrl = null, string? fileName = null)
    {
        var progress = new PptGenerationProgress
        {
            Percentage = percentage,
            Message = message,
            Status = status,
            Error = error,
            DownloadUrl = downloadUrl,
            FileName = fileName
        };

        _cache.Set(key, progress, TimeSpan.FromMinutes(10));
        _logger.LogInformation("PPT 生成进度: {Percentage}% - {Message}", percentage, message);
    }

    /// <summary>
    /// 获取进度
    /// </summary>
    public PptGenerationProgress? GetProgress(string key)
    {
        return _cache.Get<PptGenerationProgress>(key);
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

    /// <summary>
    /// 清理文件名
    /// </summary>
    private string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return sanitized.Length > 100 ? sanitized.Substring(0, 100) + ".pptx" : sanitized;
    }
}

/// <summary>
/// 幻灯片数据
/// </summary>
internal class SlideData
{
    public string Title { get; set; } = "未命名";
    public List<string> Bullets { get; set; } = new();
    public string? MermaidCode { get; set; }
    public byte[]? MermaidSvg { get; set; }
}
