namespace MyKeyVault.Web.Models.Tushare;

/// <summary>
/// Tushare 查询响应
/// </summary>
public class TushareQueryResponse
{
    public int Code { get; set; } = 0; // 0: 成功, 非0: 错误码
    public string Message { get; set; } = "success";
    public object? Data { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public bool Partial { get; set; } = false; // 是否部分数据
    public List<string> MissingItems { get; set; } = new(); // 缺失项说明
}
