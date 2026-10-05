using System.Net;
using System.Text;
using MyKeyVault.Vault.Models;

namespace MyKeyVault.Vault.Services;

public static class ArticleConversationExport
{
    public static string Markdown(KnowledgeArticle article, IReadOnlyList<ArticleExtraction> turns)
    {
        var output = new StringBuilder().Append("# ").AppendLine(PlainHeading(article.Title ?? "文章萃取对话"))
            .AppendLine().Append("文章作者：").AppendLine(PlainHeading(article.Author ?? "未知"))
            .Append("发布时间：").AppendLine(PlainHeading(article.PublishedText ?? "未知"))
            .Append("抓取时间：").AppendLine(article.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))
            .Append("对话轮次：").AppendLine(turns.Count.ToString()).AppendLine();
        for (var i = 0; i < turns.Count; i++)
        {
            var turn = turns[i];
            output.AppendLine("---").AppendLine().AppendLine($"## 第 {i + 1} 轮").AppendLine()
                .AppendLine("### 我的提问").AppendLine().AppendLine(turn.Prompt).AppendLine()
                .AppendLine("### AI 回复").AppendLine().AppendLine(turn.Result ?? "（暂无回复）").AppendLine();
            if (turn.Status != "completed" || !string.IsNullOrWhiteSpace(turn.ErrorMessage))
                output.Append("状态：").AppendLine(PlainHeading(Status(turn))).AppendLine();
        }
        return output.ToString();
    }

    public static string Html(KnowledgeArticle article, IReadOnlyList<ArticleExtraction> turns, ArticleMarkdown markdown)
    {
        var title = WebUtility.HtmlEncode(article.Title ?? "文章萃取对话");
        var output = new StringBuilder("<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'\"><title>")
            .Append(title).Append("</title><style>").Append(Styles).Append("</style></head><body><main><header><h1>").Append(title)
            .Append("</h1><p class=\"meta\">作者：").Append(WebUtility.HtmlEncode(article.Author ?? "未知"))
            .Append("　发布：").Append(WebUtility.HtmlEncode(article.PublishedText ?? "未知"))
            .Append("　抓取：").Append(article.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))
            .Append("</p><p class=\"meta\">完整对话 · ").Append(turns.Count).Append(" 轮</p></header>");
        for (var i = 0; i < turns.Count; i++)
        {
            var turn = turns[i];
            output.Append("<section class=\"turn\"><p class=\"meta\">第 ").Append(i + 1).Append(" 轮</p><h2 class=\"role\">我的提问</h2><div class=\"question\">")
                .Append(WebUtility.HtmlEncode(turn.Prompt)).Append("</div><h2 class=\"role\">AI 回复</h2><div class=\"answer\">")
                .Append(markdown.Render(turn.Result ?? "（暂无回复）")).Append("</div>");
            if (turn.Status != "completed" || !string.IsNullOrWhiteSpace(turn.ErrorMessage))
                output.Append("<p class=\"meta\">").Append(WebUtility.HtmlEncode(Status(turn))).Append("</p>");
            output.Append("</section>");
        }
        return output.Append("</main></body></html>").ToString();
    }
    private static string PlainHeading(string value) => value.Replace('\r', ' ').Replace('\n', ' ');
    private static string Status(ArticleExtraction turn) => turn.ErrorMessage ?? (turn.Status == "processing" ? "正在生成，导出内容为已保存部分。" : "本轮未完成。");
    private const string Styles = """
        *{box-sizing:border-box}body{margin:0;padding:32px 20px;color:#17212d;background:#f5f7fa;font:16px/1.85 'Avenir Next','PingFang SC','Microsoft YaHei',sans-serif}main{max-width:960px;margin:auto}header{border-bottom:1px solid #dce3ec;padding-bottom:16px}h1{font-size:1.6em;line-height:1.5}.meta{color:#697587;font-size:.82em}.turn{padding:24px 0;border-bottom:1px solid #dce3ec}.role{font-size:.82em;color:#697587;font-weight:500}.question{white-space:pre-wrap;overflow-wrap:anywhere;background:#eaf1ff;border-radius:14px;padding:14px 18px;margin:0 0 24px auto;max-width:85%;width:fit-content}.answer{overflow-wrap:anywhere}.answer h1{font-size:1.6em}.answer h2{font-size:1.35em}.answer h3{font-size:1.15em}.answer h1,.answer h2,.answer h3{line-height:1.5;margin:1.3em 0 .5em}.answer p{margin:.7em 0}.answer ul,.answer ol{padding-left:1.6em}.answer li{margin:.3em 0}.answer blockquote{margin:1em 0;padding:8px 18px;border-left:3px solid #a6bce0;background:#edf2fa;color:#506077}table{width:100%;border-collapse:collapse;table-layout:fixed;margin:1em 0}th,td{padding:9px;border:1px solid #dce3ec;text-align:left;overflow-wrap:anywhere}th{background:#eef2f6}pre{white-space:pre-wrap;overflow-wrap:anywhere;padding:16px;background:#edf1f6;border-radius:8px;font-size:.85em}:not(pre)>code{background:#e9edf3;border-radius:4px;padding:2px 5px}a{color:#2456ef}@media print{body{background:white;padding:0}.turn{break-inside:auto}a{color:inherit}}@media(max-width:600px){body{padding:16px}.question{max-width:95%}}
        """;
}
