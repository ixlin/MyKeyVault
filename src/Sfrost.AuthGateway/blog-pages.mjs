const SITE_NAME = "Ben熊的AI Space";

export function blogHomeDocument({ posts, tags, selectedTag, username }) {
  const filterName = selectedTag?.name;
  const articleList = posts.length === 0
    ? `<section class="empty"><span class="empty-mark">⌁</span><h2>${filterName ? "这个标签下还没有文章" : "从第一篇文章开始"}</h2><p>${filterName ? "换一个标签看看，或者写一篇新文章。" : "想法不需要完整，先把它保存下来。"}</p><a class="button primary" href="/blog/admin/posts/new">写第一篇</a></section>`
    : `<div class="article-grid">${posts.map(articleCard).join("")}</div>`;
  const filterBar = tags.length === 0
    ? ""
    : `<nav class="filters" aria-label="文章标签"><a class="filter ${selectedTag ? "" : "active"}" href="/blog">全部</a>${tags.map((tag) => `<a class="filter ${String(selectedTag?.id) === String(tag.id) ? "active" : ""}" href="/blog?tag=${tag.id}">${escapeHtml(tag.name)}</a>`).join("")}</nav>`;

  return blogShell({
    title: filterName ? `${filterName} · ${SITE_NAME}` : SITE_NAME,
    active: "home",
    username,
    body: `<main>
      <section class="hero">
        <div class="hero-copy">
          <p class="kicker">PRIVATE NOTES · PUBLISHED WITH INTENT</p>
          <h1>让想法先有<br><span>落脚的地方。</span></h1>
          <p class="hero-note">这里收纳正在形成的判断、实践和偶尔值得留下的瞬间。</p>
        </div>
        <a class="compose-orb" href="/blog/admin/posts/new" aria-label="写新文章"><span>＋</span><small>写文章</small></a>
      </section>
      <section class="stream-head"><div><p class="section-label">${filterName ? "FILTERED NOTES" : "LATEST NOTES"}</p><h2>${filterName ? escapeHtml(filterName) : "最近文章"}</h2></div><span>${posts.length} 篇</span></section>
      ${filterBar}
      ${articleList}
    </main>`,
  });
}

export function blogPostDocument({ post, username }) {
  return blogShell({
    title: `${post.title} · ${SITE_NAME}`,
    active: "home",
    username,
    body: `<main class="reading-main">
      <article class="reading">
        <a class="backlink" href="/blog">← 返回文章</a>
        <header class="article-header">
          <div class="article-tags">${post.tags.map(tagPill).join("")}</div>
          <h1>${escapeHtml(post.title)}</h1>
          ${post.summary ? `<p class="article-deck">${escapeHtml(post.summary)}</p>` : ""}
          <div class="article-meta"><time>${formatDate(post.published_at ?? post.updated_at, true)}</time><span>${readingMinutes(post.content)} 分钟阅读</span></div>
        </header>
        <div class="article-body">${renderArticleBody(post.content)}</div>
        <footer class="article-end"><span>⌁</span><p>写于 ${SITE_NAME}</p></footer>
      </article>
    </main>`,
  });
}

export function blogAdminDocument({ posts, stats, username, notice }) {
  const rows = posts.length === 0
    ? `<div class="admin-empty"><p>还没有文章。</p><a class="button primary" href="/blog/admin/posts/new">新建文章</a></div>`
    : `<div class="post-list">${posts.map((post) => `<article class="post-row">
        <div class="post-state ${post.status}"><span></span>${post.status === "published" ? "已发布" : "草稿"}</div>
        <div class="post-main"><h3>${escapeHtml(post.title)}</h3><p>${escapeHtml(post.summary || excerpt(post.content, 96))}</p><div class="mini-tags">${post.tags.map(tagPill).join("")}</div></div>
        <time>${formatDate(post.updated_at)}</time>
        <div class="row-actions"><a href="/blog/admin/posts/${post.id}/edit">编辑</a>${post.status === "published" ? `<a href="/blog/posts/${post.id}">查看</a>` : ""}<form method="post" action="/blog/admin/posts/${post.id}/delete" onsubmit="return confirm('确定删除这篇文章？此操作不能撤销。')"><button class="text-danger" type="submit">删除</button></form></div>
      </article>`).join("")}</div>`;

  return blogShell({
    title: `文章管理 · ${SITE_NAME}`,
    active: "admin",
    username,
    body: `<main class="admin-main">
      <header class="page-head"><div><p class="kicker">EDITORIAL DESK</p><h1>文章管理</h1><p>整理草稿，控制哪些内容出现在首页。</p></div><a class="button primary" href="/blog/admin/posts/new">＋ 新建文章</a></header>
      ${notice ? `<p class="notice success">${escapeHtml(notice)}</p>` : ""}
      <section class="stat-strip"><div><strong>${stats.total}</strong><span>全部文章</span></div><div><strong>${stats.published}</strong><span>已发布</span></div><div><strong>${stats.drafts}</strong><span>草稿</span></div><a href="/blog/admin/tags"><strong>⌗</strong><span>管理标签</span></a></section>
      ${rows}
    </main>`,
  });
}

export function blogEditorDocument({ post, tags, username, error, mode }) {
  const selectedIds = new Set((post.tags ?? []).map((tag) => String(tag.id)));
  const isEdit = mode === "edit";
  const action = isEdit ? `/blog/admin/posts/${post.id}` : "/blog/admin/posts";
  const tagOptions = tags.length === 0
    ? `<p class="form-empty">暂无标签。<a href="/blog/admin/tags">先创建标签</a></p>`
    : `<div class="tag-checks">${tags.map((tag) => `<label><input type="checkbox" name="tagId" value="${tag.id}" ${selectedIds.has(String(tag.id)) ? "checked" : ""}><span>${escapeHtml(tag.name)}</span></label>`).join("")}</div>`;

  return blogShell({
    title: `${isEdit ? "编辑文章" : "写文章"} · ${SITE_NAME}`,
    active: "write",
    username,
    body: `<main class="editor-main">
      <header class="editor-head"><div><a class="backlink" href="/blog/admin">← 返回管理</a><h1>${isEdit ? "编辑文章" : "写一篇新文章"}</h1></div><p>纯文本会被安全排版；支持以 <code>#</code> 开始的标题、<code>-</code> 列表和 <code>&gt;</code> 引用。</p></header>
      ${error ? `<p class="notice error" role="alert">${escapeHtml(error)}</p>` : ""}
      <form class="editor-form" method="post" action="${action}">
        <section class="editor-canvas">
          <label class="field title-field"><span>标题</span><input name="title" maxlength="160" value="${escapeAttribute(post.title ?? "")}" placeholder="给这篇文章一个清楚的名字" required autofocus></label>
          <label class="field"><span>摘要 <small>可选，最多 320 字</small></span><textarea name="summary" rows="3" maxlength="320" placeholder="一句话说明为什么值得读">${escapeHtml(post.summary ?? "")}</textarea></label>
          <label class="field"><span>正文</span><textarea class="content-editor" name="content" rows="22" maxlength="100000" placeholder="从这里开始……" required>${escapeHtml(post.content ?? "")}</textarea></label>
        </section>
        <aside class="editor-sidebar">
          <section class="side-card"><h2>发布状态</h2><label class="status-choice"><input type="radio" name="status" value="draft" ${(post.status ?? "draft") === "draft" ? "checked" : ""}><span><strong>保存为草稿</strong><small>只在后台可见</small></span></label><label class="status-choice"><input type="radio" name="status" value="published" ${post.status === "published" ? "checked" : ""}><span><strong>发布到首页</strong><small>登录后即可阅读</small></span></label></section>
          <section class="side-card"><div class="side-title"><h2>标签</h2><a href="/blog/admin/tags">管理</a></div>${tagOptions}</section>
          <button class="button primary wide" type="submit">${post.status === "published" ? "保存修改" : "保存文章"}</button>
        </aside>
      </form>
    </main>`,
  });
}

export function blogTagsDocument({ tags, username, error, created }) {
  const items = tags.length === 0
    ? `<div class="admin-empty"><p>还没有标签。先创建一个，用于整理文章。</p></div>`
    : `<div class="tag-admin-list">${tags.map((tag) => `<div class="tag-admin-row"><span class="tag-dot"></span><strong>${escapeHtml(tag.name)}</strong><small>${tag.post_count} 篇文章</small><form method="post" action="/blog/admin/tags/${tag.id}/delete" onsubmit="return confirm('确定删除这个标签？文章不会被删除。')"><button class="text-danger" type="submit">删除</button></form></div>`).join("")}</div>`;
  return blogShell({
    title: `标签管理 · ${SITE_NAME}`,
    active: "admin",
    username,
    body: `<main class="admin-main narrow">
      <header class="page-head"><div><a class="backlink" href="/blog/admin">← 返回文章管理</a><p class="kicker">TAXONOMY</p><h1>标签管理</h1><p>用少量、稳定的标签组织文章。</p></div></header>
      ${error ? `<p class="notice error" role="alert">${escapeHtml(error)}</p>` : ""}${created ? '<p class="notice success">标签已创建。</p>' : ""}
      <form class="tag-create" method="post" action="/blog/admin/tags"><label for="tag-name">新标签</label><div><input id="tag-name" name="name" maxlength="40" placeholder="例如：AI 实践" required autofocus><button class="button primary" type="submit">添加</button></div></form>
      ${items}
    </main>`,
  });
}

export function blogAccountDocument({ username, credentialConfigured }) {
  return blogShell({
    title: `个人中心 · ${SITE_NAME}`,
    active: "account",
    username,
    body: `<main class="account-main">
      <header class="page-head"><div><p class="kicker">PERSONAL SPACE</p><h1>个人中心</h1><p>管理进入空间的凭据，以及 AI 模型连接。</p></div></header>
      <section class="profile-card"><div class="avatar">B</div><div><small>当前账户</small><strong>${escapeHtml(username)}</strong></div><span class="secure-state">● 已安全登录</span></section>
      <div class="settings-grid">
        <a class="setting-card" href="/__sfrost-auth/account"><span class="setting-icon">⌁</span><div><h2>登录密码</h2><p>修改 SFROST 管理账户密码，并退出其他设备。</p></div><b>→</b></a>
        <a class="setting-card" href="/__sfrost-auth/models"><span class="setting-icon">◇</span><div><h2>DeepSeek 模型 Key</h2><p>${credentialConfigured ? "已配置，可替换或删除当前 Key。" : "尚未配置，配置后即可使用 Harness。"}</p></div><span class="config-state ${credentialConfigured ? "ready" : "missing"}">${credentialConfigured ? "已配置" : "未配置"}</span><b>→</b></a>
      </div>
      <form class="logout-card" method="post" action="/__sfrost-auth/logout"><div><h2>退出登录</h2><p>结束当前设备上的登录会话。</p></div><button type="submit">退出</button></form>
    </main>`,
  });
}

export function blogNotFoundDocument({ username }) {
  return blogShell({
    title: `未找到 · ${SITE_NAME}`,
    active: "",
    username,
    body: `<main><section class="empty tall"><span class="empty-mark">404</span><h1>这里没有内容</h1><p>文章可能已被撤回，或者地址发生了变化。</p><a class="button primary" href="/blog">返回首页</a></section></main>`,
  });
}

function blogShell({ title, active, username, body }) {
  return `<!doctype html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="theme-color" content="#f5f5f7">
  <title>${escapeHtml(title)}</title>
  <style>${blogStyles()}</style>
</head>
<body>
  <div class="frost-line" aria-hidden="true"></div>
  <header class="site-header">
    <nav class="nav-wrap" aria-label="主导航">
      <a class="wordmark" href="/blog"><span class="bear">B</span><span>${SITE_NAME}</span></a>
      <div class="nav-links">
        <a class="${active === "home" ? "active" : ""}" href="/blog">文章</a>
        <a class="${active === "write" ? "active" : ""}" href="/blog/admin/posts/new">写文章</a>
        <a class="${active === "admin" ? "active" : ""}" href="/blog/admin">管理</a>
        <a href="/harness">DeepSeek Harness</a>
        <a class="${active === "account" ? "active" : ""}" href="/blog/account">个人中心</a>
      </div>
      <a class="account-chip" href="/blog/account" aria-label="个人中心">${escapeHtml(username.slice(0, 1).toUpperCase())}</a>
    </nav>
  </header>
  ${body}
  <footer class="site-footer"><div><span>${SITE_NAME}</span><nav><a href="/blog">文章</a><a href="/harness">Harness</a><a href="/blog/account">个人中心</a></nav></div><a href="https://beian.miit.gov.cn/" target="_blank" rel="noopener noreferrer">蜀ICP备2024053184号</a></footer>
  <nav class="mobile-tabbar" aria-label="移动端导航">
    <a class="${active === "home" ? "active" : ""}" href="/blog"><b>文</b><span>文章</span></a>
    <a class="${active === "write" ? "active" : ""}" href="/blog/admin/posts/new"><b>＋</b><span>写作</span></a>
    <a class="${active === "admin" ? "active" : ""}" href="/blog/admin"><b>稿</b><span>管理</span></a>
    <a href="/harness"><b>AI</b><span>Harness</span></a>
    <a class="${active === "account" ? "active" : ""}" href="/blog/account"><b>我</b><span>我的</span></a>
  </nav>
</body>
</html>`;
}

function articleCard(post) {
  return `<article class="article-card"><a class="card-link" href="/blog/posts/${post.id}" aria-label="阅读 ${escapeAttribute(post.title)}"></a><div class="article-tags">${post.tags.map(tagPill).join("")}</div><h3>${escapeHtml(post.title)}</h3><p>${escapeHtml(post.summary || excerpt(post.content, 126))}</p><footer><time>${formatDate(post.published_at ?? post.updated_at)}</time><span>${readingMinutes(post.content)} 分钟阅读 →</span></footer></article>`;
}

function tagPill(tag) {
  return `<span class="tag">${escapeHtml(tag.name)}</span>`;
}

function renderArticleBody(content) {
  const lines = String(content).replaceAll("\r\n", "\n").split("\n");
  const blocks = [];
  let paragraph = [];
  let list = [];
  const flushParagraph = () => {
    if (paragraph.length > 0) blocks.push(`<p>${inlineFormat(paragraph.join(" "))}</p>`);
    paragraph = [];
  };
  const flushList = () => {
    if (list.length > 0) blocks.push(`<ul>${list.map((item) => `<li>${inlineFormat(item)}</li>`).join("")}</ul>`);
    list = [];
  };
  for (const line of lines) {
    const trimmed = line.trim();
    if (!trimmed) {
      flushParagraph();
      flushList();
    } else if (trimmed.startsWith("## ")) {
      flushParagraph(); flushList(); blocks.push(`<h2>${inlineFormat(trimmed.slice(3))}</h2>`);
    } else if (trimmed.startsWith("# ")) {
      flushParagraph(); flushList(); blocks.push(`<h2>${inlineFormat(trimmed.slice(2))}</h2>`);
    } else if (trimmed.startsWith("> ")) {
      flushParagraph(); flushList(); blocks.push(`<blockquote>${inlineFormat(trimmed.slice(2))}</blockquote>`);
    } else if (trimmed.startsWith("- ")) {
      flushParagraph(); list.push(trimmed.slice(2));
    } else {
      flushList(); paragraph.push(trimmed);
    }
  }
  flushParagraph();
  flushList();
  return blocks.join("");
}

function inlineFormat(text) {
  return escapeHtml(text).replace(/`([^`]+)`/g, "<code>$1</code>");
}

function readingMinutes(content) {
  return Math.max(1, Math.ceil(String(content).length / 500));
}

function excerpt(content, maxLength) {
  const compact = String(content).replace(/^[#>\-\s]+/gm, "").replace(/\s+/g, " ").trim();
  return compact.length > maxLength ? `${compact.slice(0, maxLength).trim()}…` : compact;
}

function formatDate(value, long = false) {
  if (!value) return "未发布";
  return new Intl.DateTimeFormat("zh-CN", {
    timeZone: "Asia/Shanghai",
    year: "numeric",
    month: long ? "long" : "2-digit",
    day: "2-digit",
  }).format(new Date(value));
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}

function escapeAttribute(value) {
  return escapeHtml(value).replaceAll("`", "&#96;");
}

function blogStyles() {
  return `
    :root{color-scheme:light;--bg:#f5f5f7;--surface:rgba(255,255,255,.76);--solid:#fff;--ink:#1d1d1f;--muted:#6e6e73;--faint:#a1a1a6;--line:rgba(0,0,0,.09);--blue:#0071e3;--blue-hover:#0077ed;--green:#248a3d;--red:#c9342f;--shadow:0 18px 55px rgba(0,0,0,.07);--radius:22px}
    *{box-sizing:border-box}html{scroll-behavior:smooth}body{margin:0;color:var(--ink);background:var(--bg);font-family:-apple-system,BlinkMacSystemFont,"SF Pro Text","PingFang SC","Helvetica Neue",sans-serif;-webkit-font-smoothing:antialiased}.frost-line{position:fixed;z-index:1000;inset:0 0 auto;height:2px;background:linear-gradient(90deg,transparent 4%,#8cd8ff 26%,#4d8dff 50%,#c3b8ff 74%,transparent 96%);opacity:.78}.site-header{position:sticky;z-index:900;top:0;border-bottom:1px solid var(--line);background:rgba(245,245,247,.78);backdrop-filter:saturate(180%) blur(22px)}.nav-wrap{width:min(1180px,calc(100% - 40px));height:64px;margin:auto;display:flex;align-items:center;gap:28px}.wordmark{display:flex;align-items:center;gap:10px;color:var(--ink);font-size:15px;font-weight:680;text-decoration:none;white-space:nowrap}.bear{display:grid;place-items:center;width:30px;height:30px;border-radius:10px;color:#fff;background:linear-gradient(145deg,#1d1d1f,#4a4a4e);font-size:14px;box-shadow:inset 0 1px rgba(255,255,255,.25)}.nav-links{display:flex;align-items:center;gap:24px;margin-left:auto}.nav-links a{position:relative;padding:22px 0 20px;color:#4a4a4e;font-size:13px;text-decoration:none;white-space:nowrap}.nav-links a:hover,.nav-links a.active{color:#000}.nav-links a.active::after{content:"";position:absolute;left:0;right:0;bottom:13px;height:2px;border-radius:2px;background:var(--blue)}.account-chip{display:grid;place-items:center;width:31px;height:31px;border-radius:50%;color:#fff;background:#1d1d1f;font-size:12px;font-weight:700;text-decoration:none}
    main{width:min(1180px,calc(100% - 40px));margin:0 auto}.hero{min-height:500px;display:flex;align-items:center;justify-content:space-between;gap:70px;padding:96px 4vw 72px}.kicker,.section-label{margin:0;color:var(--blue);font:650 11px/1.2 ui-monospace,SFMono-Regular,Menlo,monospace;letter-spacing:.16em}.hero h1{margin:22px 0 26px;font-size:clamp(58px,8.2vw,108px);font-weight:710;line-height:.91;letter-spacing:-.075em}.hero h1 span{color:#8b8b91}.hero-note{max-width:570px;margin:0;color:var(--muted);font-size:19px;line-height:1.65}.compose-orb{flex:0 0 auto;width:164px;height:164px;border-radius:50%;display:flex;flex-direction:column;align-items:center;justify-content:center;color:#fff;background:linear-gradient(145deg,#147ce5,#6aaeff);box-shadow:0 28px 70px rgba(0,113,227,.26),inset 0 1px rgba(255,255,255,.45);text-decoration:none;transition:transform .25s,box-shadow .25s}.compose-orb:hover{transform:scale(1.035) rotate(-2deg);box-shadow:0 34px 85px rgba(0,113,227,.34),inset 0 1px rgba(255,255,255,.45)}.compose-orb span{font-size:40px;font-weight:200;line-height:1}.compose-orb small{margin-top:9px;font-size:13px;font-weight:650}.stream-head{display:flex;align-items:end;justify-content:space-between;padding:40px 0 24px;border-top:1px solid var(--line)}.stream-head h2{margin:8px 0 0;font-size:34px;letter-spacing:-.04em}.stream-head>span{color:var(--faint);font-size:13px}.filters{display:flex;gap:8px;overflow:auto;padding-bottom:22px}.filter{padding:8px 13px;border:1px solid var(--line);border-radius:999px;color:#5d5d62;background:rgba(255,255,255,.6);font-size:12px;text-decoration:none;white-space:nowrap}.filter:hover,.filter.active{color:#fff;border-color:#1d1d1f;background:#1d1d1f}.article-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:18px;padding-bottom:90px}.article-card{position:relative;min-height:300px;padding:34px;border:1px solid rgba(255,255,255,.9);border-radius:var(--radius);background:var(--surface);box-shadow:0 1px 0 rgba(0,0,0,.03);backdrop-filter:blur(18px);transition:transform .25s,box-shadow .25s,border-color .25s}.article-card:hover{z-index:2;transform:translateY(-5px);border-color:#fff;box-shadow:var(--shadow)}.card-link{position:absolute;inset:0;border-radius:inherit}.article-tags,.mini-tags{display:flex;flex-wrap:wrap;gap:7px}.tag{display:inline-flex;padding:5px 9px;border-radius:999px;color:#52606d;background:#edf0f3;font-size:11px;font-weight:600}.article-card h3{max-width:15em;margin:28px 0 14px;font-size:30px;line-height:1.12;letter-spacing:-.045em}.article-card>p{max-width:42em;margin:0;color:var(--muted);font-size:15px;line-height:1.7}.article-card footer{position:absolute;left:34px;right:34px;bottom:30px;display:flex;justify-content:space-between;color:var(--faint);font-size:12px}.article-card footer span{color:#55555a}.empty{margin:0 0 90px;padding:90px 30px;border:1px dashed rgba(0,0,0,.14);border-radius:var(--radius);text-align:center;background:rgba(255,255,255,.38)}.empty.tall{margin-top:80px}.empty-mark{display:block;margin-bottom:22px;color:#9b9ba0;font:500 28px/1 ui-monospace,monospace}.empty h2,.empty h1{margin:0 0 10px;font-size:30px}.empty p{margin:0 0 26px;color:var(--muted)}
    .reading-main{max-width:920px}.reading{padding:74px 0 110px}.backlink{display:inline-block;margin-bottom:46px;color:var(--blue);font-size:13px;text-decoration:none}.article-header{padding-bottom:54px;border-bottom:1px solid var(--line)}.article-header h1{margin:24px 0 20px;font-size:clamp(46px,7vw,78px);line-height:1.02;letter-spacing:-.06em}.article-deck{max-width:760px;margin:0;color:var(--muted);font-size:21px;line-height:1.6}.article-meta{display:flex;gap:24px;margin-top:32px;color:var(--faint);font-size:12px}.article-body{max-width:720px;margin:58px auto 0;font-family:"Songti SC","STSong","Iowan Old Style",serif;font-size:19px;line-height:1.95}.article-body p{margin:0 0 1.55em}.article-body h2{margin:2.1em 0 .8em;font-family:-apple-system,BlinkMacSystemFont,"PingFang SC",sans-serif;font-size:30px;line-height:1.25;letter-spacing:-.035em}.article-body blockquote{margin:2em 0;padding:2px 0 2px 24px;border-left:3px solid #8fc8ff;color:#55555a;font-size:21px}.article-body ul{margin:0 0 1.6em;padding-left:1.4em}.article-body code{padding:.12em .34em;border-radius:5px;background:#e9e9ec;font:85% ui-monospace,SFMono-Regular,Menlo,monospace}.article-end{margin:72px auto 0;padding-top:32px;border-top:1px solid var(--line);color:var(--faint);text-align:center}.article-end span{font-size:25px}.article-end p{font-size:12px}
    .button{display:inline-flex;align-items:center;justify-content:center;height:44px;padding:0 18px;border:0;border-radius:12px;font-size:14px;font-weight:650;text-decoration:none;cursor:pointer}.button.primary{color:#fff;background:var(--blue)}.button.primary:hover{background:var(--blue-hover)}.button.wide{width:100%}.page-head{display:flex;align-items:end;justify-content:space-between;gap:30px;padding:76px 0 48px}.page-head h1{margin:12px 0 10px;font-size:52px;letter-spacing:-.055em}.page-head p:last-child{margin:0;color:var(--muted)}.admin-main{padding-bottom:90px}.admin-main.narrow{max-width:850px}.notice{margin:0 0 22px;padding:13px 16px;border-radius:12px;font-size:13px}.notice.success{color:#176529;background:#e5f5e9}.notice.error{color:#8d201d;background:#fde9e8}.stat-strip{display:grid;grid-template-columns:repeat(4,1fr);margin-bottom:24px;border:1px solid var(--line);border-radius:18px;background:rgba(255,255,255,.62);overflow:hidden}.stat-strip>div,.stat-strip>a{padding:24px;border-right:1px solid var(--line);text-decoration:none}.stat-strip>:last-child{border:0}.stat-strip strong{display:block;color:var(--ink);font-size:28px}.stat-strip span{display:block;margin-top:4px;color:var(--muted);font-size:12px}.post-list{border:1px solid var(--line);border-radius:18px;background:rgba(255,255,255,.7);overflow:hidden}.post-row{display:grid;grid-template-columns:88px minmax(0,1fr) 100px 150px;gap:22px;align-items:center;padding:24px;border-bottom:1px solid var(--line)}.post-row:last-child{border:0}.post-state{color:var(--muted);font-size:11px}.post-state span{display:inline-block;width:7px;height:7px;margin-right:7px;border-radius:50%;background:#a1a1a6}.post-state.published span{background:#34c759}.post-main h3{margin:0 0 6px;font-size:17px}.post-main>p{margin:0 0 10px;color:var(--muted);font-size:12px;line-height:1.5}.post-row>time{color:var(--faint);font-size:11px}.row-actions{display:flex;justify-content:flex-end;gap:13px}.row-actions a,.row-actions button,.text-danger{padding:0;border:0;color:var(--blue);background:none;font:12px inherit;text-decoration:none;cursor:pointer}.row-actions form{margin:0}.row-actions .text-danger,.text-danger{color:var(--red)}.admin-empty{padding:60px;text-align:center;border:1px dashed var(--line);border-radius:18px;background:rgba(255,255,255,.45)}
    .editor-main{padding:70px 0 100px}.editor-head{display:flex;justify-content:space-between;align-items:end;margin-bottom:34px}.editor-head .backlink{margin-bottom:18px}.editor-head h1{margin:0;font-size:44px;letter-spacing:-.05em}.editor-head>p{max-width:380px;margin:0;color:var(--muted);font-size:13px;line-height:1.6}.editor-head code{padding:2px 5px;border-radius:5px;background:#e7e7ea}.editor-form{display:grid;grid-template-columns:minmax(0,1fr) 300px;gap:20px;align-items:start}.editor-canvas,.side-card{border:1px solid var(--line);border-radius:20px;background:rgba(255,255,255,.78);box-shadow:0 1px rgba(255,255,255,.8)}.editor-canvas{padding:36px}.field{display:block;margin-bottom:25px}.field:last-child{margin-bottom:0}.field>span{display:flex;justify-content:space-between;margin-bottom:9px;color:#4c4c50;font-size:12px;font-weight:650}.field small{color:var(--faint);font-weight:400}.field input,.field textarea,.tag-create input{width:100%;border:1px solid #d2d2d7;border-radius:12px;outline:0;color:var(--ink);background:#fff;font:15px/1.55 inherit;transition:border-color .18s,box-shadow .18s}.field input{height:50px;padding:0 14px}.field textarea{padding:13px 14px;resize:vertical}.field input:focus,.field textarea:focus,.tag-create input:focus{border-color:#78b7f5;box-shadow:0 0 0 4px rgba(0,113,227,.1)}.title-field input{height:64px;font-size:23px;font-weight:650;letter-spacing:-.025em}.content-editor{min-height:480px;font-family:"Songti SC","STSong",serif!important;font-size:17px!important;line-height:1.8!important}.editor-sidebar{position:sticky;top:84px;display:grid;gap:14px}.side-card{padding:22px}.side-card h2{margin:0 0 16px;font-size:14px}.side-title{display:flex;justify-content:space-between}.side-title a{color:var(--blue);font-size:11px;text-decoration:none}.status-choice{display:flex;gap:10px;margin:13px 0;cursor:pointer}.status-choice input{margin-top:3px;accent-color:var(--blue)}.status-choice span{display:grid;gap:3px}.status-choice strong{font-size:13px}.status-choice small{color:var(--muted);font-size:11px}.tag-checks{display:flex;flex-wrap:wrap;gap:8px}.tag-checks label{cursor:pointer}.tag-checks input{position:absolute;opacity:0}.tag-checks span{display:inline-flex;padding:7px 10px;border:1px solid var(--line);border-radius:999px;color:var(--muted);background:#f6f6f8;font-size:11px}.tag-checks input:checked+span{color:#fff;border-color:#1d1d1f;background:#1d1d1f}.form-empty{color:var(--muted);font-size:12px}.form-empty a{color:var(--blue)}
    .tag-create{margin-bottom:20px;padding:26px;border:1px solid var(--line);border-radius:18px;background:rgba(255,255,255,.7)}.tag-create>label{display:block;margin-bottom:10px;font-size:12px;font-weight:650}.tag-create>div{display:flex;gap:10px}.tag-create input{height:44px;padding:0 13px}.tag-admin-list{border:1px solid var(--line);border-radius:18px;background:rgba(255,255,255,.7);overflow:hidden}.tag-admin-row{display:grid;grid-template-columns:12px minmax(0,1fr) 100px 50px;gap:12px;align-items:center;padding:18px 22px;border-bottom:1px solid var(--line)}.tag-admin-row:last-child{border:0}.tag-dot{width:8px;height:8px;border-radius:50%;background:#86bdf3}.tag-admin-row strong{font-size:14px}.tag-admin-row small{color:var(--faint);font-size:11px}.tag-admin-row form{text-align:right}
    .account-main{max-width:920px;padding-bottom:100px}.profile-card{display:flex;align-items:center;gap:16px;padding:24px;border:1px solid var(--line);border-radius:20px;background:rgba(255,255,255,.74)}.avatar{display:grid;place-items:center;width:52px;height:52px;border-radius:16px;color:#fff;background:linear-gradient(145deg,#1d1d1f,#5b5b60);font-weight:700}.profile-card div:nth-child(2){display:grid;gap:4px}.profile-card small{color:var(--faint);font-size:11px}.profile-card strong{font-size:15px}.secure-state{margin-left:auto;color:var(--green);font-size:11px}.settings-grid{display:grid;grid-template-columns:1fr 1fr;gap:16px;margin-top:16px}.setting-card{position:relative;min-height:180px;padding:28px;border:1px solid var(--line);border-radius:20px;color:var(--ink);background:rgba(255,255,255,.74);text-decoration:none;transition:transform .2s,box-shadow .2s}.setting-card:hover{transform:translateY(-3px);box-shadow:var(--shadow)}.setting-icon{display:grid;place-items:center;width:38px;height:38px;margin-bottom:28px;border-radius:11px;color:#fff;background:#1d1d1f}.setting-card h2{margin:0 0 8px;font-size:18px}.setting-card p{max-width:28em;margin:0;color:var(--muted);font-size:12px;line-height:1.55}.setting-card>b{position:absolute;right:24px;top:27px;color:var(--faint)}.config-state{position:absolute;right:24px;bottom:24px;font-size:11px}.config-state.ready{color:var(--green)}.config-state.missing{color:var(--red)}.logout-card{display:flex;align-items:center;justify-content:space-between;margin-top:16px;padding:24px 28px;border:1px solid var(--line);border-radius:20px;background:rgba(255,255,255,.5)}.logout-card h2{margin:0 0 5px;font-size:15px}.logout-card p{margin:0;color:var(--muted);font-size:12px}.logout-card button{border:0;color:var(--red);background:none;font-weight:650;cursor:pointer}
    .site-footer{width:min(1180px,calc(100% - 40px));margin:0 auto;padding:32px 0 42px;border-top:1px solid var(--line);display:flex;justify-content:space-between;color:var(--faint);font-size:11px}.site-footer>div{display:flex;gap:28px}.site-footer nav{display:flex;gap:16px}.site-footer a{color:var(--faint);text-decoration:none}.site-footer a:hover{color:var(--ink)}.mobile-tabbar{display:none}
    :focus-visible{outline:3px solid rgba(0,113,227,.42);outline-offset:3px}@media(max-width:900px){.nav-links{gap:15px}.nav-links a:nth-child(2),.nav-links a:nth-child(3){display:none}.hero{min-height:430px;padding:70px 0}.compose-orb{width:126px;height:126px}.article-grid{grid-template-columns:1fr}.editor-form{grid-template-columns:1fr}.editor-sidebar{position:static}.post-row{grid-template-columns:76px minmax(0,1fr)}.post-row>time{display:none}.row-actions{grid-column:2;justify-content:flex-start}.settings-grid{grid-template-columns:1fr}}@media(max-width:620px){body{padding-bottom:84px}.nav-wrap,main,.site-footer{width:min(100% - 28px,1180px)}.nav-links,.account-chip{display:none}.wordmark span:last-child{display:inline}.hero{align-items:flex-start;min-height:auto;padding:66px 0 60px}.hero h1{font-size:54px}.hero-note{font-size:16px}.compose-orb{display:none}.stream-head{padding-top:32px}.article-card{min-height:320px;padding:26px}.article-card footer{left:26px;right:26px}.reading{padding-top:50px}.article-header h1{font-size:44px}.article-body{font-size:18px}.page-head{align-items:flex-start;flex-direction:column;padding-top:54px}.page-head h1{font-size:42px}.stat-strip{grid-template-columns:1fr 1fr}.stat-strip>:nth-child(2){border-right:0}.stat-strip>:nth-child(-n+2){border-bottom:1px solid var(--line)}.post-row{grid-template-columns:1fr}.post-state,.row-actions{grid-column:1}.editor-main{padding-top:48px}.editor-head{display:block}.editor-head>p{margin-top:18px}.editor-canvas{padding:22px}.site-footer{display:grid;gap:18px}.site-footer>div{justify-content:space-between}.mobile-tabbar{position:fixed;z-index:950;right:10px;bottom:max(10px,env(safe-area-inset-bottom));left:10px;height:66px;padding:6px;border:1px solid rgba(255,255,255,.78);border-radius:22px;display:grid;grid-template-columns:repeat(5,1fr);background:rgba(250,250,252,.86);box-shadow:0 12px 38px rgba(0,0,0,.16);backdrop-filter:saturate(180%) blur(24px)}.mobile-tabbar a{display:flex;flex-direction:column;align-items:center;justify-content:center;gap:3px;border-radius:16px;color:#7a7a80;font-size:9px;text-decoration:none}.mobile-tabbar b{font-size:14px;line-height:1}.mobile-tabbar a.active{color:var(--blue);background:rgba(0,113,227,.09)}}@media(prefers-reduced-motion:reduce){*{scroll-behavior:auto!important;transition:none!important}}
  `;
}
