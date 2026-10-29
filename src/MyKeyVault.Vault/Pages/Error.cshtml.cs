using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MyKeyVault.Vault.Pages;

[IgnoreAntiforgeryToken]
public sealed class ErrorModel : PageModel
{
    public int Code { get; private set; } = 500;
    public string PageTitle { get; private set; } = "出了点问题";
    public string Message { get; private set; } = "请求暂时无法完成，请稍后重试。";

    public void OnGet(int? code)
    {
        Code = code is >= 400 and <= 599 ? code.Value : 500;
        Response.StatusCode = Code;
        (PageTitle, Message) = Code switch
        {
            404 => ("页面不存在", "这个地址没有对应的页面。它可能已经被移动，或者链接输入有误。"),
            401 or 403 => ("没有访问权限", "请先登录，或确认当前账号有权限查看这个页面。"),
            429 => ("操作太频繁", "请稍等一会儿再试。"),
            >= 500 => ("服务暂时不可用", "服务器遇到了一点问题，请稍后重试。"),
            _ => ("请求无法完成", "请返回上一页重试。")
        };
    }
}
