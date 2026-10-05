using MyKeyVault.Vault.Models;
using MyKeyVault.Vault.Services;
using HtmlAgilityPack;

var article = new KnowledgeArticle { Title = "测试文章 <script>title()</script>", Author = "测试作者", PublishedText = "2026-10-05", CreatedAtUtc = DateTime.UtcNow };
var turns = new List<ArticleExtraction>
{
    new() { Prompt = "第一问\n不要漏掉换行", Result = "## 标题\n\n**粗体**与*斜体*\n\n- 列表一\n- 列表二", Status = "completed" },
    new() { Prompt = "第二问 <img src=x onerror=alert(1)>", Result = "> 引用\n\n| A | B |\n|---|---|\n| 1 | 2 |\n\n```js\nconst a = 1;\n```\n<script>bad()</script>\n[x](javascript:alert(1))", Status = "completed" },
    new() { Prompt = "第三问", Result = "部分回答", Status = "cancelled", ErrorMessage = "已停止" }
};
var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
var md = ArticleConversationExport.Markdown(article, turns);
foreach (var turn in turns) Check(md.Contains(turn.Prompt) && md.Contains(turn.Result!), "Markdown retains exact question and answer");
Check(md.IndexOf("第一问") < md.IndexOf("第二问") && md.IndexOf("第二问") < md.IndexOf("第三问"), "all rounds in order");
Check(md.Contains("对话轮次：3") && md.Contains("状态：已停止"), "round count and partial-result status included");
var html = ArticleConversationExport.Html(article, turns, new ArticleMarkdown());
var doc = new HtmlDocument(); doc.LoadHtml(html);
Check(doc.DocumentNode.SelectNodes("//section[@class='turn']").Count == 3, "HTML contains every round");
Check(doc.DocumentNode.SelectNodes("//div[@class='question']").Count == 3, "HTML contains every question");
Check(doc.DocumentNode.SelectSingleNode("//strong") != null && doc.DocumentNode.SelectSingleNode("//em") != null && doc.DocumentNode.SelectSingleNode("//ul") != null, "emphasis and lists formatted");
Check(doc.DocumentNode.SelectSingleNode("//blockquote") != null && doc.DocumentNode.SelectSingleNode("//table") != null && doc.DocumentNode.SelectSingleNode("//pre/code") != null, "quotes tables and code formatted");
Check(doc.DocumentNode.SelectSingleNode("//script|//img") is null && !html.Contains("href=\"javascript:"), "untrusted metadata and content cannot execute");
Check(html.Contains("<style>") && !html.Contains("<script src") && !html.Contains("<link rel"), "standalone styling without external resources");
Check(doc.DocumentNode.InnerText.Contains("部分回答") && doc.DocumentNode.InnerText.Contains("已停止"), "partial answer is retained honestly");
Console.WriteLine($"{checks} export checks passed.");
