using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MyKeyVault.Web.Data;
using MyKeyVault.Web.Models;

namespace MyKeyVault.Web.Services;

/// <summary>
/// Tushare 认证服务
/// </summary>
public class TushareAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly TushareOptions _options;
    private readonly ILogger<TushareAuthService> _logger;

    public TushareAuthService(
        ApplicationDbContext context,
        IOptions<TushareOptions> options,
        ILogger<TushareAuthService> logger)
    {
        _context = context;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 创建新的 App
    /// </summary>
    public async Task<(TushareApp app, string plainSecret)> CreateAppAsync(string? userId = null)
    {
        var appId = GenerateAppId();
        var plainSecret = GenerateAppSecret();
        var encryptedSecret = EncryptSecret(plainSecret);

        var app = new TushareApp
        {
            AppId = appId,
            EncryptedSecret = encryptedSecret,
            UserId = userId,
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TushareApps.Add(app);
        await _context.SaveChangesAsync();

        _logger.LogInformation("创建 Tushare App: {AppId}, UserId: {UserId}", appId, userId ?? "system");

        return (app, plainSecret);
    }

    /// <summary>
    /// 验证 AppId 和 AppSecret，返回 JWT
    /// </summary>
    public async Task<string?> AuthenticateAsync(string appId, string appSecret)
    {
        var app = await _context.TushareApps
            .FirstOrDefaultAsync(a => a.AppId == appId);

        if (app == null)
        {
            _logger.LogWarning("AppId 不存在: {AppId}", appId);
            return null;
        }

        if (app.Status != "active")
        {
            _logger.LogWarning("App 已禁用: {AppId}", appId);
            return null;
        }

        var decryptedSecret = DecryptSecretWithFallback(app);
        if (decryptedSecret == null)
        {
            _logger.LogWarning("AppSecret 解密失败（可能密钥已轮换）: {AppId}", appId);
            return null;
        }
        if (decryptedSecret != appSecret)
        {
            _logger.LogWarning("AppSecret 不匹配: {AppId}", appId);
            return null;
        }

        // 生成 JWT
        var token = GenerateJwtToken(app);
        _logger.LogInformation("认证成功: {AppId}", appId);

        return token;
    }

    /// <summary>
    /// 生成 JWT Token
    /// </summary>
    private string GenerateJwtToken(TushareApp app)
    {
        var claims = new[]
        {
            new Claim("app_id", app.AppId),
            new Claim("user_id", app.UserId ?? "system"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "MyKeyVault.Tushare",
            audience: "TushareClient",
            claims: claims,
            expires: DateTime.UtcNow.AddSeconds(_options.JwtExpiresInSeconds),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// 加密 AppSecret
    /// </summary>
    private string EncryptSecret(string plainText)
    {
        using var aes = Aes.Create();
        var key = DeriveKey(_options.EncryptionKey);
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encrypted = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // IV + Encrypted
        var result = new byte[aes.IV.Length + encrypted.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// 解密（支持从旧空密钥迁移到当前密钥）
    /// </summary>
    private string? DecryptSecretWithFallback(TushareApp app)
    {
        // 首先使用当前密钥尝试解密
        try
        {
            return DecryptUsingKey(app.EncryptedSecret, _options.EncryptionKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "使用当前 EncryptionKey 解密失败，尝试回退旧密钥 (AppId={AppId})", app.AppId);
        }

        // 如果当前密钥非空，则尝试使用旧的空字符串密钥（早期版本可能用空密钥创建）
        if (!string.IsNullOrEmpty(_options.EncryptionKey))
        {
            try
            {
                var legacy = DecryptUsingKey(app.EncryptedSecret, string.Empty);
                // 如果成功，自动升级重新加密
                _logger.LogInformation("检测到旧密钥格式，自动升级 AppSecret 加密: {AppId}", app.AppId);
                app.EncryptedSecret = EncryptSecret(legacy); // 使用当前密钥重新加密
                app.UpdatedAt = DateTime.UtcNow;
                _context.TushareApps.Update(app);
                _context.SaveChanges();
                return legacy;
            }
            catch (Exception ex2)
            {
                _logger.LogWarning(ex2, "回退旧密钥解密仍失败 (AppId={AppId})", app.AppId);
            }
        }

        return null; // 解密完全失败
    }

    private string DecryptUsingKey(string cipherText, string keyMaterial)
    {
        var fullCipher = Convert.FromBase64String(cipherText);
        using var aes = Aes.Create();
        var key = DeriveKey(keyMaterial);
        aes.Key = key;
        var iv = new byte[aes.IV.Length];
        var cipher = new byte[fullCipher.Length - iv.Length];
        Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        var decrypted = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        return Encoding.UTF8.GetString(decrypted);
    }

    private byte[] DeriveKey(string password)
    {
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
    }

    private string GenerateAppId()
    {
        return $"app_{Guid.NewGuid():N}";
    }

    private string GenerateAppSecret()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}
