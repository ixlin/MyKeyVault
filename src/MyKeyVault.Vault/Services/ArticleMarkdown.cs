using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Markdig;

namespace MyKeyVault.Vault.Services;

public sealed class ArticleMarkdown
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UsePipeTables().UseEmphasisExtras().DisableHtml().Build();
    private static readonly HashSet<string> Tags = new("p br hr h1 h2 h3 h4 h5 h6 strong em del ul ol li blockquote pre code table thead tbody tr th td a".Split(' '));
    private static readonly Regex LiteralEmphasis = new(@"\*\*(.+?)\*\*", RegexOptions.Singleline | RegexOptions.Compiled);
    public string Render(string? text)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(Markdown.ToHtml(text ?? "", Pipeline));
        foreach (var node in doc.DocumentNode.Descendants().Where(x => x.NodeType == HtmlNodeType.Element).ToArray())
        {
            if (!Tags.Contains(node.Name)) { node.Remove(); continue; }
            var href = System.Net.WebUtility.HtmlDecode(node.GetAttributeValue("href", ""));
            foreach (var attribute in node.Attributes.ToArray()) node.Attributes.Remove(attribute);
            if (node.Name == "a" && Uri.TryCreate(href, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http")
            {
                node.SetAttributeValue("href", uri.AbsoluteUri);
                node.SetAttributeValue("target", "_blank");
                node.SetAttributeValue("rel", "noopener noreferrer");
            }
        }
        EmphasizeLiteralAsterisks(doc);
        return doc.DocumentNode.InnerHtml;
    }

    // CommonMark's flanking rules do not parse **bold** when the closing
    // delimiter is preceded by CJK punctuation (e.g. **“数据飞轮”**).
    // Repair leftover literal **...** in text nodes without touching code.
    private static void EmphasizeLiteralAsterisks(HtmlDocument doc)
    {
        foreach (var node in doc.DocumentNode.Descendants().OfType<HtmlTextNode>().ToArray())
        {
            if (node.Ancestors().Any(a => a.Name is "code" or "pre")) continue;
            var value = node.Text;
            if (!value.Contains("**", StringComparison.Ordinal)) continue;
            var matches = LiteralEmphasis.Matches(value);
            if (matches.Count == 0) continue;

            var parent = node.ParentNode;
            var index = parent.ChildNodes.IndexOf(node);
            if (index < 0) continue;
            parent.ChildNodes.RemoveAt(index);
            var cursor = 0;
            foreach (Match match in matches)
            {
                if (match.Index > cursor) parent.ChildNodes.Insert(index++, doc.CreateTextNode(value.Substring(cursor, match.Index - cursor)));
                var strong = doc.CreateElement("strong");
                strong.AppendChild(doc.CreateTextNode(match.Groups[1].Value));
                parent.ChildNodes.Insert(index++, strong);
                cursor = match.Index + match.Length;
            }
            if (cursor < value.Length) parent.ChildNodes.Insert(index, doc.CreateTextNode(value.Substring(cursor)));
        }
    }
}
