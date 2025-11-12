using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Web.Data;
using MyKeyVault.Web.Services;

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
    /// Tushare Token 配置
    /// </summary>
    [HttpGet("settings")]
    public IActionResult Settings()
    {
        // 优先读取环境变量（生产可通过环境变量覆盖）
        var envToken = Environment.GetEnvironmentVariable("TUSHARE__TOKEN");
        string token = string.Empty;

        if (!string.IsNullOrWhiteSpace(envToken))
        {
            token = envToken.Trim();
        }
        else
        {
            try
            {
                var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
                var fileName = env == "Development" ? "appsettings.Development.json" : "appsettings.json";
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);

                if (System.IO.File.Exists(filePath))
                {
                    var json = System.IO.File.ReadAllText(filePath);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("Tushare", out var tushareElem)
                        && tushareElem.ValueKind == System.Text.Json.JsonValueKind.Object
                        && tushareElem.TryGetProperty("Token", out var tokenElem)
                        && tokenElem.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        token = tokenElem.GetString() ?? string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "读取已保存的 Tushare Token 失败");
            }
        }

        ViewBag.TushareToken = token; // 传递到视图用于预填充
        return View();
    }

    /// <summary>
    /// 保存 Tushare Token
    /// </summary>
    [HttpPost("settings/save-token")]
    public async Task<IActionResult> SaveToken([FromBody] SaveTokenRequest request)
    {
        try
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            var fileName = env == "Development" ? "appsettings.Development.json" : "appsettings.json";
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);

            string json;
            if (System.IO.File.Exists(filePath))
            {
                json = await System.IO.File.ReadAllTextAsync(filePath);
            }
            else
            {
                json = "{}";
            }

            var settings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                ?? new Dictionary<string, object>();

            // 读取或初始化 Tushare 节
            Dictionary<string, object> tushareDict;
            if (settings.TryGetValue("Tushare", out var existing))
            {
                if (existing is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    tushareDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(je.GetRawText())
                                  ?? new Dictionary<string, object>();
                }
                else if (existing is Dictionary<string, object> d)
                {
                    tushareDict = new Dictionary<string, object>(d);
                }
                else
                {
                    tushareDict = new Dictionary<string, object>();
                }
            }
            else
            {
                tushareDict = new Dictionary<string, object>();
            }

            // 更新 Token
            tushareDict["Token"] = request.Token;

            // 设置默认值（若缺失）
            if (!tushareDict.ContainsKey("BaseUrl"))
                tushareDict["BaseUrl"] = "http://api.tushare.pro";
            if (!tushareDict.ContainsKey("JwtSecret"))
                tushareDict["JwtSecret"] = GenerateRandomKey();
            if (!tushareDict.ContainsKey("JwtExpiresInSeconds"))
                tushareDict["JwtExpiresInSeconds"] = 7200;
            if (!tushareDict.ContainsKey("EncryptionKey"))
                tushareDict["EncryptionKey"] = GenerateRandomKey();

            settings["Tushare"] = tushareDict;

            var options = new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            json = System.Text.Json.JsonSerializer.Serialize(settings, options);

            await System.IO.File.WriteAllTextAsync(filePath, json);

            return Ok(new { success = true, message = "配置已保存，请重启应用使配置生效" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存 Tushare Token 失败");
            return BadRequest(new { success = false, message = $"保存失败: {ex.Message}" });
        }
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

public class SaveTokenRequest
{
    public string Token { get; set; } = string.Empty;
}
