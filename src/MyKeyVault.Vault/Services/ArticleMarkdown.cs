using HtmlAgilityPack;
using Markdig;

namespace MyKeyVault.Vault.Services;

public sealed class ArticleMarkdown
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UsePipeTables().UseEmphasisExtras().DisableHtml().Build();
    private static readonly HashSet<string> Tags = new("p br hr h1 h2 h3 h4 h5 h6 strong em del ul ol li blockquote pre code table thead tbody tr th td a".Split(' '));
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
        return doc.DocumentNode.InnerHtml;
    }
}
