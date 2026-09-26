import { markdownStyles } from "./markdown.mjs";

const SITE_NAME = "Ben熊的AI Space";

export function kbHomeDocument({ docs, username, query = "", imported = 0, notice = "" }) {
  const rows = docs.map(documentRow).join("");
  const resultCopy = query
    ? `找到 ${docs.length} 份与“${escapeHtml(query)}”相关的文档。`
    : `${docs.length} 份文档，全部保存在服务器私有空间。`;
  const empty = query
    ? `<section class="empty"><span>⌕</span><h2>没有匹配的文档</h2><p>换一个关键词，或者返回查看全部文档。</p><a href="/kb">查看全部</a></section>`
    : `<section class="empty"><span>⌁</span><h2>知识库还是空的</h2><p>Harness 生成文件后会自动发布到这里，并返回需要登录的外网链接。</p></section>`;
  const message = notice || (imported > 0 ? `已接收 ${imported} 份 Harness 文档。` : "");
  return shell({
    title: `知识库 · ${SITE_NAME}`,
    username,
    active: "kb",
    body: `<main>
      <section class="kb-head">
        <div><p class="kicker">PRIVATE KNOWLEDGE SHELF</p><h1>知识库</h1><p>${resultCopy}</p></div>
        <form class="search" method="get" action="/kb" role="search"><label><span>搜索文档</span><input type="search" name="q" value="${escapeAttribute(query)}" maxlength="80" placeholder="标题或文件名"></label><button type="submit">搜索</button></form>
      </section>
      ${message ? `<p class="notice" role="status">${escapeHtml(message)}</p>` : ""}
      <section class="shelf" aria-label="知识库文档">
        <header><span>文档</span><span>状态</span><span>操作</span></header>
        ${docs.length > 0 ? rows : empty}
      </section>
      <aside class="kb-note"><strong>发布规则</strong><p>HTML、Word、PDF 等产物由 Harness 提交后自动归档。文档默认不做向量处理；后续只有真正接入向量检索后，才会开放“向量入库”操作。</p></aside>
    </main>`,
  });
}

export function kbDocumentDocument({ document, username, markdownHtml }) {
  const preview = previewFor(document, markdownHtml);
  return shell({
    title: `${document.title} · 知识库`,
    username,
    active: "kb",
    body: `<main class="document-main">
      <a class="back" href="/kb">← 返回知识库</a>
      <header class="document-head"><div><p class="kicker">${escapeHtml(kindFor(document.mime_type))} · ${formatSize(document.size_bytes)}</p><h1>${escapeHtml(document.title)}</h1><p>${escapeHtml(document.original_name)} · ${formatDate(document.created_at)}</p></div><a class="download" href="/kb/d/${document.id}/download">下载原文件</a></header>
      ${preview}
    </main>`,
  });
}

export function kbNotFoundDocument({ username }) {
  return shell({
    title: `未找到 · 知识库`,
    username,
    active: "kb",
    body: `<main><section class="empty tall"><span>404</span><h1>没有找到这份文档</h1><p>它可能尚未发布、已被删除，或者链接有误。</p><a href="/kb">返回知识库</a></section></main>`,
  });
}

function documentRow(document) {
  return `<article class="doc-row">
    <span class="spine ${spineClass(document.mime_type)}" aria-hidden="true"></span>
    <a class="doc-main" href="/kb/d/${document.id}"><small>${escapeHtml(kindFor(document.mime_type))} · ${formatSize(document.size_bytes)}</small><strong>${escapeHtml(document.title)}</strong><span>${escapeHtml(document.original_name)} · ${formatDate(document.created_at)}</span></a>
    <span class="vector-state">○ 未向量化</span>
    <div class="actions"><a href="/kb/d/${document.id}">打开</a><a href="/kb/d/${document.id}/download">下载</a><form method="post" action="/kb/d/${document.id}/delete" onsubmit="return confirm('确定删除这份文档？此操作不能撤销。')"><input type="hidden" name="confirmation" value="DELETE"><button type="submit">删除</button></form></div>
  </article>`;
}

function previewFor(document, markdownHtml) {
  const source = `/kb/raw/${document.id}`;
  if (document.mime_type.startsWith("text/markdown")) {
    return markdownHtml === null
      ? `<section class="download-card"><div><h2>文件较大，请下载阅读</h2><p>在线排版支持 2 MB 以内的 Markdown 文档。</p></div><a class="download" href="/kb/d/${document.id}/download">下载原文件</a></section>`
      : `<article class="markdown-document formatted-markdown">${markdownHtml}</article>`;
  }
  if (document.mime_type.startsWith("text/html")) {
    return `<section class="preview-shell"><div class="preview-bar"><i></i><i></i><i></i><span>安全 HTML 预览</span></div><iframe title="${escapeAttribute(document.title)}" src="${source}" sandbox></iframe></section>`;
  }
  if (document.mime_type === "application/pdf") {
    return `<section class="preview-shell"><div class="preview-bar"><i></i><i></i><i></i><span>PDF 预览</span></div><iframe title="${escapeAttribute(document.title)}" src="${source}"></iframe></section>`;
  }
  if (document.mime_type.startsWith("text/")) {
    return `<section class="preview-shell text-preview"><div class="preview-bar"><i></i><i></i><i></i><span>文本预览</span></div><iframe title="${escapeAttribute(document.title)}" src="${source}" sandbox></iframe></section>`;
  }
  return `<section class="download-card"><span>${escapeHtml(kindFor(document.mime_type))}</span><div><h2>该格式需要下载后查看</h2><p>文件保存在服务器私有目录，下载请求同样需要登录。</p></div><a class="download" href="/kb/d/${document.id}/download">下载 ${escapeHtml(document.original_name)}</a></section>`;
}

function shell({ title, username, active, body }) {
  return `<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="theme-color" content="#f5f5f7"><title>${escapeHtml(title)}</title><style>${styles()}${markdownStyles()}.markdown-document{max-width:900px;margin:0 auto;padding:48px min(6vw,76px) 70px;border:1px solid var(--line);border-radius:18px;background:#fff;box-shadow:0 18px 55px rgba(0,0,0,.06)}.document-head h1{font-size:clamp(34px,4.5vw,56px);line-height:1.12}@media(max-width:620px){.markdown-document{padding:24px 20px 42px;border-radius:14px}}</style></head><body><div class="frost-line" aria-hidden="true"></div><header class="site-header"><nav class="nav-wrap" aria-label="主导航"><a class="wordmark" href="/"><span class="bear">B</span><span>${SITE_NAME}</span></a><div class="nav-links"><a href="/">工作台</a><a class="${active === "kb" ? "active" : ""}" href="/kb">知识库</a><a href="/blog">BLOG</a><a href="/blog/admin">后台</a><a href="/harness">Harness</a><a href="/blog/account">个人中心</a></div><a class="account-chip" href="/blog/account" aria-label="个人中心">${escapeHtml(username.slice(0, 1).toUpperCase())}</a></nav></header>${body}<footer class="site-footer"><span>${SITE_NAME}</span><a href="https://beian.miit.gov.cn/" target="_blank" rel="noopener noreferrer">蜀ICP备2024053184号</a></footer><nav class="mobile-tabbar" aria-label="移动端导航"><a href="/"><b>⌂</b><span>首页</span></a><a class="active" href="/kb"><b>库</b><span>知识</span></a><a href="/blog"><b>文</b><span>BLOG</span></a><a href="/harness"><b>AI</b><span>Harness</span></a><a href="/blog/account"><b>我</b><span>我的</span></a></nav></body></html>`;
}

function styles() {
  return `:root{color-scheme:light;--bg:#f5f5f7;--surface:rgba(255,255,255,.78);--ink:#1d1d1f;--muted:#6e6e73;--faint:#a1a1a6;--line:rgba(0,0,0,.09);--blue:#0071e3;--red:#c9342f;--green:#248a3d;--radius:22px}*{box-sizing:border-box}html{scroll-behavior:smooth}body{margin:0;color:var(--ink);background:var(--bg);font-family:-apple-system,BlinkMacSystemFont,"SF Pro Text","PingFang SC","Helvetica Neue",sans-serif;-webkit-font-smoothing:antialiased}.frost-line{position:fixed;z-index:1000;inset:0 0 auto;height:2px;background:linear-gradient(90deg,transparent 4%,#8cd8ff 26%,#4d8dff 50%,#c3b8ff 74%,transparent 96%);opacity:.78}.site-header{position:sticky;z-index:900;top:0;border-bottom:1px solid var(--line);background:rgba(245,245,247,.78);backdrop-filter:saturate(180%) blur(22px)}.nav-wrap{width:min(1180px,calc(100% - 40px));height:64px;margin:auto;display:flex;align-items:center;gap:28px}.wordmark{display:flex;align-items:center;gap:10px;color:var(--ink);font-size:15px;font-weight:680;text-decoration:none;white-space:nowrap}.bear{display:grid;place-items:center;width:30px;height:30px;border-radius:10px;color:#fff;background:linear-gradient(145deg,#1d1d1f,#4a4a4e);font-size:14px}.nav-links{display:flex;align-items:center;gap:22px;margin-left:auto}.nav-links a{position:relative;padding:22px 0 20px;color:#4a4a4e;font-size:13px;text-decoration:none;white-space:nowrap}.nav-links a:hover,.nav-links a.active{color:#000}.nav-links a.active:after{content:"";position:absolute;left:0;right:0;bottom:13px;height:2px;border-radius:2px;background:var(--blue)}.account-chip{display:grid;place-items:center;width:31px;height:31px;border-radius:50%;color:#fff;background:#1d1d1f;font-size:12px;font-weight:700;text-decoration:none}main{width:min(1180px,calc(100% - 40px));margin:0 auto;padding-bottom:92px}.kb-head{display:flex;align-items:end;justify-content:space-between;gap:48px;padding:82px 0 42px}.kicker{margin:0;color:var(--blue);font:650 11px/1.2 ui-monospace,SFMono-Regular,Menlo,monospace;letter-spacing:.16em}.kb-head h1,.document-head h1{margin:13px 0 10px;font-size:clamp(48px,7vw,78px);line-height:.96;letter-spacing:-.065em}.kb-head p:not(.kicker),.document-head p{margin:0;color:var(--muted);font-size:14px}.search{display:flex;gap:9px;padding:7px;border:1px solid var(--line);border-radius:15px;background:rgba(255,255,255,.72)}.search label span{position:absolute;width:1px;height:1px;overflow:hidden;clip:rect(0,0,0,0)}.search input{width:230px;height:38px;padding:0 11px;border:0;outline:0;background:transparent;font:14px inherit}.search button,.download{height:38px;padding:0 15px;border:0;border-radius:10px;color:#fff;background:var(--blue);font:650 13px/38px inherit;text-decoration:none;cursor:pointer}.notice{margin:0 0 18px;padding:13px 16px;border-radius:12px;color:#176529;background:#e5f5e9;font-size:13px}.shelf{border:1px solid var(--line);border-radius:20px;background:var(--surface);box-shadow:0 1px rgba(255,255,255,.9);overflow:hidden}.shelf>header{display:grid;grid-template-columns:minmax(0,1fr) 130px 190px;gap:20px;padding:13px 22px 13px 40px;border-bottom:1px solid var(--line);color:var(--faint);font-size:10px;text-transform:uppercase;letter-spacing:.12em}.doc-row{position:relative;display:grid;grid-template-columns:minmax(0,1fr) 130px 190px;gap:20px;align-items:center;min-height:104px;padding:18px 22px 18px 40px;border-bottom:1px solid var(--line)}.doc-row:last-child{border-bottom:0}.spine{position:absolute;left:15px;width:7px;height:58px;border-radius:7px;background:#8e8e93;box-shadow:inset 0 1px rgba(255,255,255,.55)}.spine.html{background:linear-gradient(#5ac8fa,#007aff)}.spine.word{background:linear-gradient(#64d2ff,#0a84ff)}.spine.pdf{background:linear-gradient(#ff6961,#d70015)}.spine.data{background:linear-gradient(#30d158,#248a3d)}.doc-main{display:grid;gap:5px;min-width:0;color:var(--ink);text-decoration:none}.doc-main small{color:var(--blue);font:650 10px/1.2 ui-monospace,monospace;letter-spacing:.08em}.doc-main strong{overflow:hidden;text-overflow:ellipsis;font-size:17px;white-space:nowrap}.doc-main span{overflow:hidden;color:var(--muted);font-size:11px;text-overflow:ellipsis;white-space:nowrap}.vector-state{color:var(--faint);font-size:11px}.actions{display:flex;align-items:center;justify-content:flex-end;gap:12px}.actions a,.actions button{padding:0;border:0;color:var(--blue);background:none;font:12px inherit;text-decoration:none;cursor:pointer}.actions button{color:var(--red)}.actions form{margin:0}.kb-note{display:grid;grid-template-columns:120px minmax(0,1fr);gap:24px;margin-top:22px;padding:22px 24px;border:1px solid var(--line);border-radius:18px;color:var(--muted);background:rgba(255,255,255,.46);font-size:12px;line-height:1.65}.kb-note strong{color:var(--ink)}.kb-note p{margin:0}.empty{grid-column:1/-1;padding:82px 24px;text-align:center}.empty.tall{margin-top:80px;border:1px dashed var(--line);border-radius:20px}.empty>span{color:var(--faint);font:28px ui-monospace,monospace}.empty h2,.empty h1{margin:18px 0 8px}.empty p{margin:0 0 22px;color:var(--muted)}.empty a,.back{color:var(--blue);text-decoration:none}.document-main{padding-top:58px}.back{display:inline-block;margin-bottom:38px;font-size:13px}.document-head{display:flex;align-items:end;justify-content:space-between;gap:30px;padding-bottom:38px}.document-head h1{max-width:900px;font-size:clamp(40px,6vw,68px)}.preview-shell{height:min(72vh,860px);min-height:520px;border:1px solid var(--line);border-radius:20px;background:#fff;box-shadow:0 18px 55px rgba(0,0,0,.08);overflow:hidden}.preview-bar{height:44px;display:flex;align-items:center;gap:7px;padding:0 16px;border-bottom:1px solid var(--line);background:#f0f0f2}.preview-bar i{width:11px;height:11px;border-radius:50%;background:#ff5f57}.preview-bar i:nth-child(2){background:#febc2e}.preview-bar i:nth-child(3){background:#28c840}.preview-bar span{margin-left:8px;color:var(--muted);font-size:11px}.preview-shell iframe{width:100%;height:calc(100% - 44px);border:0;background:#fff}.download-card{display:flex;align-items:center;gap:24px;padding:38px;border:1px solid var(--line);border-radius:22px;background:var(--surface)}.download-card>span{display:grid;place-items:center;width:72px;height:88px;border-radius:12px;color:#fff;background:linear-gradient(145deg,#147ce5,#6aaeff);font-weight:700}.download-card div{flex:1}.download-card h2{margin:0 0 8px}.download-card p{margin:0;color:var(--muted);font-size:13px}.site-footer{width:min(1180px,calc(100% - 40px));margin:auto;padding:32px 0 42px;border-top:1px solid var(--line);display:flex;justify-content:space-between;color:var(--faint);font-size:11px}.site-footer a{color:var(--faint);text-decoration:none}.mobile-tabbar{display:none}:focus-visible{outline:3px solid rgba(0,113,227,.42);outline-offset:3px}@media(max-width:900px){.nav-links{gap:14px}.nav-links a:nth-child(4){display:none}.kb-head{align-items:flex-start;flex-direction:column}.doc-row,.shelf>header{grid-template-columns:minmax(0,1fr) 110px}.actions{grid-column:1/-1;justify-content:flex-start}.shelf>header span:last-child{display:none}.document-head{align-items:flex-start;flex-direction:column}.preview-shell{min-height:480px}}@media(max-width:620px){body{padding-bottom:84px}.nav-wrap,main,.site-footer{width:min(100% - 28px,1180px)}.nav-links,.account-chip{display:none}.kb-head{padding:58px 0 32px;gap:24px}.kb-head h1{font-size:52px}.search{width:100%}.search label{flex:1}.search input{width:100%}.shelf>header{display:none}.doc-row{display:block;padding:22px 22px 22px 38px}.spine{left:14px;top:23px;height:68px}.vector-state{display:block;margin:12px 0}.actions{justify-content:flex-start}.kb-note{grid-template-columns:1fr}.document-main{padding-top:42px}.document-head{padding-bottom:28px}.document-head h1{font-size:42px}.preview-shell{min-height:520px;border-radius:15px}.download-card{align-items:flex-start;flex-wrap:wrap;padding:26px}.download-card .download{width:100%;text-align:center}.site-footer{display:flex}.mobile-tabbar{position:fixed;z-index:950;right:10px;bottom:max(10px,env(safe-area-inset-bottom));left:10px;height:66px;padding:6px;border:1px solid rgba(255,255,255,.78);border-radius:22px;display:grid;grid-template-columns:repeat(5,1fr);background:rgba(250,250,252,.86);box-shadow:0 12px 38px rgba(0,0,0,.16);backdrop-filter:saturate(180%) blur(24px)}.mobile-tabbar a{display:flex;flex-direction:column;align-items:center;justify-content:center;gap:3px;border-radius:16px;color:#7a7a80;font-size:9px;text-decoration:none}.mobile-tabbar b{font-size:14px;line-height:1}.mobile-tabbar a.active{color:var(--blue);background:rgba(0,113,227,.09)}}@media(prefers-reduced-motion:reduce){*{scroll-behavior:auto!important;transition:none!important}}`;
}

function kindFor(mimeType) {
  if (mimeType.includes("html")) return "HTML";
  if (mimeType.includes("markdown")) return "Markdown";
  if (mimeType.includes("word") || mimeType.includes("msword")) return "Word";
  if (mimeType.includes("pdf")) return "PDF";
  if (mimeType.includes("sheet")) return "Excel";
  if (mimeType.includes("presentation")) return "PowerPoint";
  if (mimeType.startsWith("text/")) return "文本";
  return "文件";
}

function spineClass(mimeType) {
  if (mimeType.includes("html")) return "html";
  if (mimeType.includes("word")) return "word";
  if (mimeType.includes("pdf")) return "pdf";
  if (mimeType.includes("sheet") || mimeType.includes("csv")) return "data";
  return "";
}

function formatSize(value) {
  const bytes = Number(value);
  if (!Number.isFinite(bytes) || bytes < 1) return "—";
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

function formatDate(value) {
  return new Intl.DateTimeFormat("zh-CN", { year: "numeric", month: "short", day: "numeric", hour: "2-digit", minute: "2-digit", hour12: false }).format(new Date(value));
}

function escapeHtml(value) {
  return String(value ?? "").replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&#39;");
}

function escapeAttribute(value) {
  return escapeHtml(value).replaceAll("`", "&#96;");
}
