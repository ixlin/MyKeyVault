import MarkdownIt from "markdown-it";

const markdown = new MarkdownIt({ html: false, linkify: true, breaks: false });
const mediaPath = /^\/blog\/media\/[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

markdown.validateLink = (url) => /^(https?:\/\/|mailto:|\/(?!\/)|#)/i.test(url);

const defaultLinkOpen = markdown.renderer.rules.link_open
  ?? ((tokens, index, options, environment, renderer) => renderer.renderToken(tokens, index, options));
markdown.renderer.rules.link_open = (tokens, index, options, environment, renderer) => {
  const url = tokens[index].attrGet("href") ?? "";
  if (/^https?:\/\//i.test(url)) {
    tokens[index].attrSet("target", "_blank");
    tokens[index].attrSet("rel", "noopener noreferrer");
  }
  return defaultLinkOpen(tokens, index, options, environment, renderer);
};

markdown.renderer.rules.image = (tokens, index) => {
  const token = tokens[index];
  const source = token.attrGet("src") ?? "";
  const alt = token.content || "图片";
  if (!/^https:\/\//i.test(source) && !mediaPath.test(source)) {
    return escapeHtml(alt);
  }
  return `<img src="${escapeHtml(source)}" alt="${escapeHtml(alt)}" loading="lazy" referrerpolicy="no-referrer">`;
};

export function renderMarkdown(source) {
  return markdown.render(String(source ?? ""));
}

export function markdownStyles() {
  return `.formatted-markdown{max-width:760px;margin-inline:auto;color:#242428;font-family:-apple-system,BlinkMacSystemFont,"PingFang SC","Helvetica Neue",sans-serif;font-size:17px;line-height:1.86;overflow-wrap:anywhere}.formatted-markdown>*:first-child{margin-top:0}.formatted-markdown p,.formatted-markdown ul,.formatted-markdown ol,.formatted-markdown blockquote,.formatted-markdown pre,.formatted-markdown table{margin:0 0 1.45em}.formatted-markdown h1,.formatted-markdown h2,.formatted-markdown h3,.formatted-markdown h4{font-family:inherit;line-height:1.3;letter-spacing:-.025em;scroll-margin-top:90px}.formatted-markdown h1{margin:1.7em 0 .65em;font-size:2.05em}.formatted-markdown h2{margin:1.8em 0 .65em;padding-bottom:.24em;border-bottom:1px solid #e7e7eb;font-size:1.55em}.formatted-markdown h3{margin:1.6em 0 .5em;font-size:1.24em}.formatted-markdown h4{margin:1.4em 0 .4em;font-size:1.06em}.formatted-markdown strong{font-weight:710}.formatted-markdown em{font-style:italic}.formatted-markdown ul,.formatted-markdown ol{padding-left:1.55em}.formatted-markdown li{padding-left:.2em;margin:.25em 0}.formatted-markdown li>p{margin:.35em 0}.formatted-markdown blockquote{padding:.1em 0 .1em 1.15em;border-left:3px solid #86b9ed;color:#5d626a}.formatted-markdown blockquote>:last-child{margin-bottom:0}.formatted-markdown a{color:#0666c7;text-decoration:underline;text-underline-offset:3px}.formatted-markdown img{display:block;max-width:100%;height:auto;margin:1.4em auto;border-radius:12px}.formatted-markdown code{padding:.16em .35em;border-radius:5px;background:#eff1f5;font:87%/1.5 ui-monospace,SFMono-Regular,Menlo,monospace}.formatted-markdown pre{overflow-x:auto;padding:17px 19px;border-radius:12px;color:#e8edf6;background:#202735;font:13px/1.65 ui-monospace,SFMono-Regular,Menlo,monospace}.formatted-markdown pre code{padding:0;color:inherit;background:transparent;font:inherit}.formatted-markdown table{display:block;max-width:100%;overflow-x:auto;border-collapse:collapse;font-size:.91em}.formatted-markdown th,.formatted-markdown td{min-width:90px;padding:9px 13px;border:1px solid #dfe3ea;text-align:left;vertical-align:top}.formatted-markdown th{background:#f0f3f7;font-weight:650}.formatted-markdown hr{height:1px;margin:2em 0;border:0;background:#e1e4e9}@media(max-width:620px){.formatted-markdown{font-size:16px;line-height:1.8}.formatted-markdown h1{font-size:1.7em}.formatted-markdown h2{font-size:1.35em}}`;
}

function escapeHtml(value) {
  return String(value).replaceAll("&", "&amp;").replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&#39;");
}
