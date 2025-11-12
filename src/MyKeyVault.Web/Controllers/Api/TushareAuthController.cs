using Microsoft.AspNetCore.Mvc;
using MyKeyVault.Web.Models.Tushare;
using MyKeyVault.Web.Services;
using Microsoft.Extensions.Options;

namespace MyKeyVault.Web.Controllers.Api;

[ApiController]
[Route("api/tushare/auth")]
public class TushareAuthController : ControllerBase
{
    private readonly TushareAuthService _authService;
    private readonly ILogger<TushareAuthController> _logger;
    private readonly TushareOptions _options;

    public TushareAuthController(
        TushareAuthService authService,
        ILogger<TushareAuthController> logger,
        IOptions<TushareOptions> options)
    {
        _authService = authService;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// 获取访问令牌
    /// </summary>
    [HttpPost("token")]
    public async Task<IActionResult> GetToken([FromBody] AuthTokenRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (string.IsNullOrWhiteSpace(_options.JwtSecret))
            {
                return StatusCode(503, new { error = "config_missing", error_description = "JwtSecret 未配置或为空，请在系统设置中保存并重启应用后再试" });
            }

            var token = await _authService.AuthenticateAsync(request.AppId, request.AppSecret);

            if (token == null)
            {
                return Unauthorized(new { error = "invalid_credentials", error_description = "AppId 或 AppSecret 不正确，或应用已被禁用" });
            }

            var response = new AuthTokenResponse
            {
                AccessToken = token,
                ExpiresIn = _options.JwtExpiresInSeconds,
                TokenType = "Bearer"
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取访问令牌失败");
            return StatusCode(500, new { error = "server_error", error_description = ex.Message });
        }
    }
}
