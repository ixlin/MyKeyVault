using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MyKeyVault.Web.Services;

/// <summary>
/// Tushare 官方 API 调用服务
/// </summary>
public class TushareApiService
{
    private readonly HttpClient _httpClient;
    private readonly TushareOptions _options;
    private readonly ILogger<TushareApiService> _logger;

    public TushareApiService(
        HttpClient httpClient,
        IOptions<TushareOptions> options,
        ILogger<TushareApiService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 调用 Tushare API
    /// </summary>
    public async Task<TushareApiResponse> CallApiAsync(string apiName, Dictionary<string, object> parameters)
    {
        var request = new
        {
            api_name = apiName,
            token = _options.Token,
            @params = parameters,
            fields = "" // 返回所有字段
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation("调用 Tushare API: {ApiName}, 参数: {Params}", apiName, JsonSerializer.Serialize(parameters));

        var response = await _httpClient.PostAsync(_options.BaseUrl, content);
        var responseText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Tushare API 调用失败: {StatusCode}, 响应: {Response}", response.StatusCode, responseText);
            return new TushareApiResponse
            {
                Code = -1,
                Msg = $"HTTP {response.StatusCode}: {responseText}"
            };
        }

        var result = JsonSerializer.Deserialize<TushareApiResponse>(responseText, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (result == null)
        {
            return new TushareApiResponse { Code = -1, Msg = "解析响应失败" };
        }

        if (result.Code != 0)
        {
            _logger.LogWarning("Tushare API 返回错误: {Code}, {Msg}", result.Code, result.Msg);
        }

        return result;
    }
}

/// <summary>
/// Tushare API 原始响应
/// </summary>
public class TushareApiResponse
{
    public int Code { get; set; }
    public string Msg { get; set; } = string.Empty;
    public TushareApiData? Data { get; set; }
}

public class TushareApiData
{
    public List<string> Fields { get; set; } = new();
    public List<List<JsonElement>> Items { get; set; } = new();
}
