using Microsoft.AspNetCore.Identity.UI.Services;
using System.Net;
using System.Net.Mail;

namespace MyKeyVault.Web.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailSender> _logger;

        public EmailSender(IConfiguration configuration, ILogger<EmailSender> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            _logger.LogInformation("📧 [EMAIL] 开始发送邮件。收件人: {Email}, 主题: {Subject}", email, subject);
            
            try
            {
                var smtpHost = _configuration["Email:SmtpHost"];
                var smtpPort = _configuration.GetValue<int>("Email:SmtpPort", 587);
                var smtpUser = _configuration["Email:SmtpUser"];
                var smtpPassword = _configuration["Email:SmtpPassword"];
                var fromEmail = _configuration["Email:FromEmail"];
                var fromName = _configuration["Email:FromName"];

                // 如果没有配置SMTP信息，记录日志但不发送
                if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpUser))
                {
                    _logger.LogWarning("⚠️ [EMAIL] SMTP配置不完整，邮件发送被跳过。收件人: {Email}, 主题: {Subject}", email, subject);
                    _logger.LogInformation("📄 [EMAIL] 邮件内容预览:\n收件人: {Email}\n主题: {Subject}\n内容: {HtmlMessage}", email, subject, htmlMessage);
                    return;
                }

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(smtpUser, smtpPassword)
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail ?? smtpUser, fromName ?? "MyKeyVault"),
                    Subject = subject,
                    Body = htmlMessage,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(email);

                _logger.LogInformation("📤 [EMAIL] 正在通过SMTP发送邮件...");
                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("✅ [EMAIL] 邮件发送成功。收件人: {Email}, 主题: {Subject}", email, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [EMAIL] 邮件发送失败。收件人: {Email}, 主题: {Subject}", email, subject);
                // 在开发环境可以抛出异常，生产环境建议记录日志但不中断流程
                if (_configuration.GetValue<bool>("Email:ThrowOnError", false))
                {
                    throw;
                }
            }
        }
    }
}
