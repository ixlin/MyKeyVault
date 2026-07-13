namespace MyKeyVault.Web.Services;

/// <summary>微信小程序服务端凭据。AppSecret 只能保存在服务器私密配置中。</summary>
public class WechatMiniProgramOptions
{
    public const string SectionName = "WechatMiniProgram";

    public string AppId { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
}
