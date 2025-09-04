using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace MyKeyVault.Web.Controllers
{
    public class TestController : Controller
    {
        private readonly IEmailSender _emailSender;
        private readonly ILogger<TestController> _logger;

        public TestController(IEmailSender emailSender, ILogger<TestController> logger)
        {
            _emailSender = emailSender;
            _logger = logger;
        }

        // 测试邮件发送功能的端点
        // 访问: /Test/SendTestEmail?email=test@example.com
        public async Task<IActionResult> SendTestEmail(string email = "test@example.com")
        {
            try
            {
                _logger.LogInformation("🧪 [TEST] 开始测试邮件发送...");
                
                await _emailSender.SendEmailAsync(
                    email,
                    "测试邮件",
                    "<h1>这是一封测试邮件</h1><p>如果您收到这封邮件，说明邮件服务配置正确。</p>"
                );

                _logger.LogInformation("🧪 [TEST] 测试邮件发送完成");
                return Ok($"测试邮件已发送到 {email}，请检查控制台日志。");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🧪 [TEST] 测试邮件发送失败");
                return BadRequest($"测试邮件发送失败: {ex.Message}");
            }
        }
    }
}
