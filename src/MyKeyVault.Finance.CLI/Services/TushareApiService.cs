using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MyKeyVault.Finance.CLI.Models;

namespace MyKeyVault.Finance.CLI.Services;

public class TushareApiService
{
    private readonly HttpClient _httpClient;
    private readonly ApiSettings _apiSettings;
    private string? _accessToken;

    public TushareApiService(ApiSettings apiSettings)
    {
        _apiSettings = apiSettings;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(apiSettings.BaseUrl)
        };
    }

    /// <summary>
    /// 获取访问令牌
    /// </summary>
    public async Task<bool> AuthenticateAsync()
    {
        try
        {
            var request = new AuthTokenRequest
            {
                AppId = _apiSettings.AppId,
                AppSecret = _apiSettings.AppSecret
            };

            var response = await _httpClient.PostAsJsonAsync("/api/tushare/auth/token", request);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"认证失败: {error}");
                return false;
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<AuthTokenResponse>();
            if (tokenResponse != null)
            {
                _accessToken = tokenResponse.AccessToken;
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", _accessToken);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"认证异常: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 查询 Tushare 数据
    /// </summary>
    public async Task<TushareQueryResponse?> QueryAsync(string apiName, Dictionary<string, object> parameters)
    {
        try
        {
            if (string.IsNullOrEmpty(_accessToken))
            {
                Console.WriteLine("未认证,请先调用 AuthenticateAsync");
                return null;
            }

            var request = new TushareQueryRequest
            {
                ApiName = apiName,
                Params = parameters,
                ForceRefresh = false
            };

            var response = await _httpClient.PostAsJsonAsync("/api/tushare/query", request);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"查询失败: {error}");
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<TushareQueryResponse>();
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"查询异常: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 转换股票代码格式 (600036 -> 600036.SH)
    /// </summary>
    public string ConvertStockCode(string simpleCode)
    {
        // 6开头是上海主板, 0/3开头是深圳
        if (simpleCode.StartsWith("6"))
            return $"{simpleCode}.SH";
        else if (simpleCode.StartsWith("0") || simpleCode.StartsWith("3"))
            return $"{simpleCode}.SZ";
        else
            return simpleCode; // 已经是完整格式或其他格式
    }
}
