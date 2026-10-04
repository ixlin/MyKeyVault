using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace MyKeyVault.Vault.Services;

public sealed class ResetEmailOptions
{
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 465;
    public string SmtpUser { get; set; } = "";
    public string SmtpPassword { get; set; } = "";
    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "我的密码本";
}

public interface IPasswordResetEmailSender
{
    bool IsConfigured { get; }
    Task SendCodeAsync(string email, string code, CancellationToken cancellationToken);
    Task SendChangedNoticeAsync(string email, CancellationToken cancellationToken);
}

public sealed class PasswordResetEmailSender(IOptions<ResetEmailOptions> options) : IPasswordResetEmailSender
{
    private ResetEmailOptions Settings => options.Value;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Settings.SmtpHost)
        && !string.IsNullOrWhiteSpace(Settings.SmtpUser) && !string.IsNullOrWhiteSpace(Settings.SmtpPassword)
        && !string.IsNullOrWhiteSpace(Settings.FromEmail) && Settings.SmtpPort is > 0 and <= 65535;

    public Task SendCodeAsync(string email, string code, CancellationToken cancellationToken) => SendAsync(email,
        "我的密码本：重置密码验证码", $"你正在重置 mykeyvault.cn 的登录密码。\n\n验证码：{code}\n\n10 分钟内有效，仅可使用一次。请勿向任何人提供验证码。\n如果不是你本人操作，请忽略此邮件。", cancellationToken);

    public Task SendChangedNoticeAsync(string email, CancellationToken cancellationToken) => SendAsync(email,
        "我的密码本：登录密码已重置", "你的 mykeyvault.cn 登录密码刚刚通过邮箱验证重置。\n密码本内的数据未变更。\n如果不是你本人操作，请立即联系网站管理员并保护你的邮箱账号。", cancellationToken);

    private async Task SendAsync(string email, string subject, string body, CancellationToken cancellationToken)
    {
        if (!IsConfigured) throw new InvalidOperationException("Password reset email is not configured.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var message = new MimeMessage();
        message.From.Add(new MailboxAddress(Settings.FromName, Settings.FromEmail));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };
        using var client = new SmtpClient { Timeout = 20000 };
        // Never allow plaintext SMTP or bypass certificate validation.
        await client.ConnectAsync(Settings.SmtpHost, Settings.SmtpPort,
            Settings.SmtpPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, timeout.Token);
        await client.AuthenticateAsync(Settings.SmtpUser, Settings.SmtpPassword, timeout.Token);
        await client.SendAsync(message, timeout.Token);
        await client.DisconnectAsync(true, timeout.Token);
    }
}
