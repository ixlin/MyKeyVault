using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MyKeyVault.Web.Services;

public sealed class WechatMiniProgramService
{
    private readonly HttpClient _httpClient;
    private readonly WechatMiniProgramOptions _options;
    private readonly ILogger<WechatMiniProgramService> _logger;

    public WechatMiniProgramService(
        HttpClient httpClient,
        IOptions<WechatMiniProgramOptions> options,
        ILogger<WechatMiniProgramService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GetOpenIdAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.AppId) || string.IsNullOrWhiteSpace(_options.AppSecret))
        {
            throw new InvalidOperationException("微信小程序服务端凭据尚未配置。");
        }

        var url = "https://api.weixin.qq.com/sns/jscode2session"
            + $"?appid={Uri.EscapeDataString(_options.AppId)}"
            + $"&secret={Uri.EscapeDataString(_options.AppSecret)}"
            + $"&js_code={Uri.EscapeDataString(code)}&grant_type=authorization_code";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;

        var hasWechatError = root.TryGetProperty("errcode", out var errorCode);
        if (!response.IsSuccessStatusCode || hasWechatError)
        {
            var codeValue = hasWechatError && errorCode.ValueKind == JsonValueKind.Number ? errorCode.GetInt32() : 0;
            _logger.LogWarning("微信 code 换取 OpenID 失败，状态码 {StatusCode}，微信错误码 {WechatErrorCode}", response.StatusCode, codeValue);
            throw new InvalidOperationException("微信授权已失效，请重新尝试。");
        }

        if (!root.TryGetProperty("openid", out var openIdElement) || string.IsNullOrWhiteSpace(openIdElement.GetString()))
        {
            throw new InvalidOperationException("微信未返回有效身份标识。");
        }

        return openIdElement.GetString()!;
    }
}
