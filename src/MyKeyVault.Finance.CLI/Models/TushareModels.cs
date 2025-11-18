using System.Text.Json;

namespace MyKeyVault.Finance.CLI.Models;

public class AuthTokenRequest
{
    public string AppId { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
}

public class AuthTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public string TokenType { get; set; } = string.Empty;
}

public class TushareQueryRequest
{
    public string ApiName { get; set; } = string.Empty;
    public Dictionary<string, object> Params { get; set; } = new();
    public bool ForceRefresh { get; set; } = false;
}

public class TushareQueryResponse
{
    public int Code { get; set; }
    public string? Message { get; set; }
    // 使用 JsonElement? 明确类型，避免 dynamic 比较导致运行时绑定错误
    public JsonElement? Data { get; set; }
}
