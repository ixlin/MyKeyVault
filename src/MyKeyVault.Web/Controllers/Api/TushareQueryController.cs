using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyKeyVault.Web.Models.Tushare;
using MyKeyVault.Web.Services;

namespace MyKeyVault.Web.Controllers.Api;

[ApiController]
[Route("api/tushare")]
[Authorize(AuthenticationSchemes = "TushareBearer")]
public class TushareQueryController : ControllerBase
{
    private readonly TushareDataService _dataService;
    private readonly ILogger<TushareQueryController> _logger;

    public TushareQueryController(
        TushareDataService dataService,
        ILogger<TushareQueryController> logger)
    {
        _dataService = dataService;
        _logger = logger;
    }

    /// <summary>
    /// 统一查询接口
    /// </summary>
    [HttpPost("query")]
    public async Task<IActionResult> Query([FromBody] TushareQueryRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // 从 JWT 中提取 AppId
        var appId = User.FindFirst("app_id")?.Value;
        if (string.IsNullOrEmpty(appId))
        {
            return Unauthorized(new { error = "invalid_token", error_description = "Token 中缺少 app_id" });
        }

        var response = await _dataService.QueryAsync(appId, request);

        if (response.Code != 0)
        {
            return StatusCode(500, response);
        }

        return Ok(response);
    }
}
