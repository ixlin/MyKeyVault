using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Web.Data;
using MyKeyVault.Web.Services;
using Microsoft.Extensions.Hosting;

namespace MyKeyVault.Web.Controllers;

[Authorize(Roles = "Admin")]
[Route("tushare/management")]
public class TushareManagementController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly TushareAuthService _authService;
    private readonly ILogger<TushareManagementController> _logger;

    public TushareManagementController(
        ApplicationDbContext context,
        TushareAuthService authService,
        ILogger<TushareManagementController> logger)
    {
        _context = context;
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// 应用管理首页
    /// </summary>
    [HttpGet("apps")]
    public async Task<IActionResult> Apps()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var apps = await _context.TushareApps
            .Where(a => a.UserId == userId && !a.IsDeleted) // 过滤已删除的应用
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return View(apps);
    }

    /// <summary>
    /// 创建新应用
    /// </summary>
    [HttpPost("apps/create")]
    public async Task<IActionResult> CreateApp(string? description)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var (app, plainSecret) = await _authService.CreateAppAsync(userId);

        // 设置备注
        if (!string.IsNullOrWhiteSpace(description))
        {
            app.Description = description.Trim();
            await _context.SaveChangesAsync();
        }

        TempData["NewAppId"] = app.AppId;
        TempData["NewAppSecret"] = plainSecret;
        TempData["SuccessMessage"] = "应用创建成功！请妥善保存 AppSecret，它只会显示一次。";

        return RedirectToAction(nameof(Apps));
    }

    /// <summary>
    /// 禁用应用
    /// </summary>
    [HttpPost("apps/{id}/disable")]
    public async Task<IActionResult> DisableApp(long id)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var app = await _context.TushareApps.FindAsync(id);

        if (app == null || app.UserId != userId)
        {
            return NotFound();
        }

        app.Status = "disabled";
        app.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "应用已禁用";
        return RedirectToAction(nameof(Apps));
    }

    /// <summary>
    /// 启用应用
    /// </summary>
    [HttpPost("apps/{id}/enable")]
    public async Task<IActionResult> EnableApp(long id)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var app = await _context.TushareApps.FindAsync(id);

        if (app == null || app.UserId != userId)
        {
            return NotFound();
        }

        app.Status = "active";
        app.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "应用已启用";
        return RedirectToAction(nameof(Apps));
    }

    /// <summary>
    /// 编辑应用备注
    /// </summary>
    [HttpPost("apps/{id}/edit")]
    public async Task<IActionResult> EditApp(long id, string? description)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var app = await _context.TushareApps.FindAsync(id);

        if (app == null || app.UserId != userId || app.IsDeleted)
        {
            return NotFound();
        }

        app.Description = description?.Trim();
        app.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "应用备注已更新";
        return RedirectToAction(nameof(Apps));
    }

    /// <summary>
    /// 软删除应用
    /// </summary>
    [HttpPost("apps/{id}/delete")]
    public async Task<IActionResult> DeleteApp(long id)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var app = await _context.TushareApps.FindAsync(id);

        if (app == null || app.UserId != userId || app.IsDeleted)
        {
            return NotFound();
        }

        app.IsDeleted = true;
        app.DeletedAt = DateTime.UtcNow;
        app.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "应用已删除";
        return RedirectToAction(nameof(Apps));
    }

    /// <summary>
    /// 调用日志
    /// </summary>
    [HttpGet("logs")]
    public async Task<IActionResult> Logs(int page = 1, int pageSize = 50)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        var appIds = await _context.TushareApps
            .Where(a => a.UserId == userId)
            .Select(a => a.AppId)
            .ToListAsync();

        var query = _context.CallLogs
            .Where(l => appIds.Contains(l.AppId))
            .OrderByDescending(l => l.RequestAt);

        var total = await query.CountAsync();
        var logs = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.Total = total;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);

        return View(logs);
    }

    /// <summary>
    /// API 文档
    /// </summary>
    [HttpGet("api-docs")]
    public IActionResult ApiDocs()
    {
        return View();
    }

    /// <summary>
    /// API 测试工具
    /// </summary>
    [HttpGet("test-tool")]
    public IActionResult TestTool()
    {
        return View();
    }

    /// <summary>
    /// Tushare 配置页面
    /// </summary>
    [HttpGet("settings")]
    public IActionResult Settings()
    {
        var config = new TushareConfigViewModel
        {
            Token = string.Empty,
            BaseUrl = "http://api.tushare.pro",
            JwtSecret = string.Empty,
            JwtExpiresInSeconds = 7200,
            EncryptionKey = string.Empty
        };

        try
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            // 读取文件优先级：
            // Development: appsettings.Development.json -> appsettings.json
            // Production:  appsettings.Production.json  -> appsettings.json
            var candidates = env == "Development"
                ? new[] { "appsettings.Development.json", "appsettings.json" }
                : new[] { "appsettings.Production.json", "appsettings.json" };

            foreach (var file in candidates)
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), file);
                if (!System.IO.File.Exists(filePath)) continue;

                var json = System.IO.File.ReadAllText(filePath);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("Tushare", out var tushareElem)
                    && tushareElem.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    if (tushareElem.TryGetProperty("Token", out var tokenElem))
                        config.Token = tokenElem.GetString() ?? string.Empty;
                    
                    if (tushareElem.TryGetProperty("BaseUrl", out var baseUrlElem))
                        config.BaseUrl = baseUrlElem.GetString() ?? "http://api.tushare.pro";
                    
                    if (tushareElem.TryGetProperty("JwtSecret", out var jwtSecretElem))
                        config.JwtSecret = jwtSecretElem.GetString() ?? string.Empty;
                    
                    if (tushareElem.TryGetProperty("JwtExpiresInSeconds", out var expiresElem))
                        config.JwtExpiresInSeconds = expiresElem.TryGetInt32(out var exp) ? exp : 7200;
                    
                    if (tushareElem.TryGetProperty("EncryptionKey", out var encKeyElem))
                        config.EncryptionKey = encKeyElem.GetString() ?? string.Empty;
                    
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取已保存的 Tushare 配置失败");
        }

        ViewBag.TushareConfig = config;
        return View();
    }

    /// <summary>
    /// 保存 Tushare 配置
    /// </summary>
    [HttpPost("settings/save")]
    public async Task<IActionResult> SaveSettings([FromBody] SaveTushareConfigRequest request)
    {
        var (success, message) = await SaveSettingsInternal(request);
        if (success) return Ok(new { success = true, message = message });
        return BadRequest(new { success = false, message });
    }

    /// <summary>
    /// 保存并重启（通过停止当前进程，交由 systemd 重启）
    /// </summary>
    [HttpPost("settings/save-and-restart")]
    public async Task<IActionResult> SaveSettingsAndRestart([FromBody] SaveTushareConfigRequest request)
    {
        var (success, message) = await SaveSettingsInternal(request);
        if (!success) return BadRequest(new { success = false, message });

        // 异步延迟后优雅停止应用，期望由 systemd 接管重启
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1200);
                var lifetime = HttpContext.RequestServices.GetRequiredService<IHostApplicationLifetime>();
                _logger.LogWarning("[ADMIN] Save-and-restart requested by {User}. Stopping application...", User?.Identity?.Name);
                lifetime.StopApplication();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StopApplication failed in save-and-restart");
            }
        });

        return Ok(new { success = true, message = "配置已保存，正在重启服务...（约数秒内恢复）" });
    }

    private async Task<(bool success, string message)> SaveSettingsInternal(SaveTushareConfigRequest request)
    {
        try
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            // 写入文件优先级：
            // Development: appsettings.Development.json
            // Production:  appsettings.Production.json（若不存在则回落到 appsettings.json）
            string fileName;
            if (env == "Development")
                fileName = "appsettings.Development.json";
            else
            {
                var prodPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.Production.json");
                fileName = System.IO.File.Exists(prodPath) ? "appsettings.Production.json" : "appsettings.json";
            }

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);

            string json = System.IO.File.Exists(filePath)
                ? await System.IO.File.ReadAllTextAsync(filePath)
                : "{}";

            var settings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                ?? new Dictionary<string, object>();

            // 读取或初始化 Tushare 节
            Dictionary<string, object> tushareDict;
            if (settings.TryGetValue("Tushare", out var existing))
            {
                if (existing is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Object)
                    tushareDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(je.GetRawText())
                                  ?? new Dictionary<string, object>();
                else if (existing is Dictionary<string, object> d)
                    tushareDict = new Dictionary<string, object>(d);
                else
                    tushareDict = new Dictionary<string, object>();
            }
            else
            {
                tushareDict = new Dictionary<string, object>();
            }

            // 更新配置项
            tushareDict["Token"] = request.Token ?? string.Empty;
            tushareDict["BaseUrl"] = request.BaseUrl ?? "http://api.tushare.pro";
            tushareDict["JwtExpiresInSeconds"] = request.JwtExpiresInSeconds;

            if (!string.IsNullOrWhiteSpace(request.JwtSecret))
                tushareDict["JwtSecret"] = request.JwtSecret;
            else if (!tushareDict.ContainsKey("JwtSecret") || string.IsNullOrWhiteSpace(tushareDict["JwtSecret"]?.ToString()))
                tushareDict["JwtSecret"] = GenerateRandomKey();

            if (!string.IsNullOrWhiteSpace(request.EncryptionKey))
                tushareDict["EncryptionKey"] = request.EncryptionKey;
            else if (!tushareDict.ContainsKey("EncryptionKey") || string.IsNullOrWhiteSpace(tushareDict["EncryptionKey"]?.ToString()))
                tushareDict["EncryptionKey"] = GenerateRandomKey();

            settings["Tushare"] = tushareDict;

            var options = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            json = System.Text.Json.JsonSerializer.Serialize(settings, options);

            await System.IO.File.WriteAllTextAsync(filePath, json);
            return (true, "配置已保存，请重启应用使配置生效");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存 Tushare 配置失败");
            return (false, $"保存失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 生成随机密钥（用于表单上的生成按钮）
    /// </summary>
    [HttpPost("settings/generate-key")]
    public IActionResult GenerateKey()
    {
        return Ok(new { key = GenerateRandomKey() });
    }

    private string GenerateRandomKey()
    {
        var bytes = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// 数据统计
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> Stats()
    {
        var stockBasicCount = await _context.StockBasics.CountAsync();
        var stockDailyCount = await _context.StockDailies.CountAsync();
        var incomeCount = await _context.IncomeStatements.CountAsync();
        var balanceCount = await _context.BalanceSheets.CountAsync();
        var cashflowCount = await _context.CashflowStatements.CountAsync();

        ViewBag.StockBasicCount = stockBasicCount;
        ViewBag.StockDailyCount = stockDailyCount;
        ViewBag.IncomeCount = incomeCount;
        ViewBag.BalanceCount = balanceCount;
        ViewBag.CashflowCount = cashflowCount;

        return View();
    }
}

public class SaveTushareConfigRequest
{
    public string? Token { get; set; }
    public string? BaseUrl { get; set; }
    public string? JwtSecret { get; set; }
    public int JwtExpiresInSeconds { get; set; } = 7200;
    public string? EncryptionKey { get; set; }
}

public class TushareConfigViewModel
{
    public string Token { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string JwtSecret { get; set; } = string.Empty;
    public int JwtExpiresInSeconds { get; set; }
    public string EncryptionKey { get; set; } = string.Empty;
}
