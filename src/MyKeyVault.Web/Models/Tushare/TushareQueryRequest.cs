namespace MyKeyVault.Web.Models.Tushare;

/// <summary>
/// Tushare 查询请求
/// </summary>
public class TushareQueryRequest
{
    public string ApiName { get; set; } = string.Empty;
    public Dictionary<string, object> Params { get; set; } = new();
    public bool ForceRefresh { get; set; } = false;
}
