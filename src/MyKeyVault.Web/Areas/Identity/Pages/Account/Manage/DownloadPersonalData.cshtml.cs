using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Web.Data;
using MyKeyVault.Web.Models;

namespace MyKeyVault.Web.Areas.Identity.Pages.Account.Manage;

public class DownloadPersonalDataModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public DownloadPersonalDataModel(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
        }

        // 1. 获取用户的所有标签
        var userTags = await _context.Tags
            .Where(t => t.UserId == user.Id)
            .Select(t => new TagExportModel
            {
                Id = t.TagId,
                Name = t.TagName,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();

        // 2. 获取用户的所有账户及其关联的标签名
        var userAccountsQuery = await _context.Accounts
            .Where(a => a.UserId == user.Id)
            .Include(a => a.AccountTags)
            .ThenInclude(at => at.Tag)
            .ToListAsync();

        var userAccounts = userAccountsQuery.Select(a => new AccountExportModel
        {
            Id = a.AccountId,
            Title = a.Title,
            Username = a.AccountNameEncrypted,
            Website = a.Url,
            EncryptedPassword = a.PasswordEncrypted,
            Notes = a.NoteEncrypted,
            Tags = a.AccountTags.Where(at => at.Tag != null).Select(at => at.Tag!.TagName).ToList(),
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt
        }).ToList();

        // 3. 构建最终的导出数据结构
        var exportData = new ExportData
        {
            ExportInfo = new ExportInfo
            {
                AppName = "MyKeyVault",
                ExportDate = DateTime.UtcNow
            },
            UserProfile = new UserProfile
            {
                Id = user.Id,
                Email = user.Email,
                CreatedAt = user.CreatedAt
            },
            Tags = userTags,
            Accounts = userAccounts
        };

        // 4. 序列化为 JSON 并返回文件
        var options = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(exportData, options);

        return File(jsonBytes, "application/json", "mykeyvault_data.json");
    }
}

// --- 数据导出模型 ---

public class ExportData
{
    public ExportInfo? ExportInfo { get; set; }
    public UserProfile? UserProfile { get; set; }
    public List<TagExportModel> Tags { get; set; } = new();
    public List<AccountExportModel> Accounts { get; set; } = new();
}

public class ExportInfo
{
    public string AppName { get; set; } = "MyKeyVault";
    public DateTime ExportDate { get; set; }
}

public class UserProfile
{
    public string? Id { get; set; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TagExportModel
{
    public long Id { get; set; }
    public string? Name { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AccountExportModel
{
    public long Id { get; set; }
    public string? Title { get; set; }
    public string? Username { get; set; }
    public string? Website { get; set; }
    public string? EncryptedPassword { get; set; }
    public string? Notes { get; set; }
    public List<string> Tags { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
