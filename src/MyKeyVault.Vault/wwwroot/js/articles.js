(() => {
  'use strict';
  const element = (tag, text, className) => { const el = document.createElement(tag); if (text != null) el.textContent = text; if (className) el.className = className; return el; };
  const library = document.querySelector('[data-article-library]');
  const names = { completed: '已收录', failed: '失败', cancelled: '已取消', pending: '排队中', processing: '处理中' };
  document.querySelector('[data-capture-form]')?.addEventListener('submit', event => {
    const button = event.target.querySelector('button[type=submit]'); button.disabled = true; button.textContent = '正在提交…';
  });
  if (library) {
    let failures = 0;
    const poll = async () => {
      if (!library.querySelector('[data-state="pending"], [data-state="processing"]')) return;
      if (document.hidden) { setTimeout(poll, 5000); return; }
      try {
        const response = await fetch(library.dataset.progressUrl, { headers: { Accept: 'application/json' }, cache: 'no-store' });
        if (!response.ok || !response.headers.get('content-type')?.includes('application/json')) throw new Error('进度暂时无法更新，请检查网络或重新登录。');
        const data = await response.json(); failures = 0;
        library.querySelector('[data-progress-notice]').textContent = '';
        for (const item of data.articles) {
          const row = library.querySelector(`[data-article-id="${Number(item.id)}"]`); if (!row) continue;
          row.dataset.state = item.status;
          row.querySelector('[data-article-title]').textContent = item.title || (item.status === 'failed' ? '抓取未完成' : '正在获取文章标题…');
          row.querySelector('[data-article-author]').textContent = item.author || '公众号文章';
          row.querySelector('[data-article-resources]').textContent = `${item.images} 张图片 · ${item.extractionCount} 次萃取`;
          const badge = row.querySelector('[data-article-state]'); badge.textContent = names[item.status] || item.status;
          badge.className = 'status-pill status-' + (Object.hasOwn(names, item.status) ? item.status : 'pending');
          const active = ['pending', 'processing'].includes(item.status);
          row.querySelector('[data-progress-block]').hidden = !active;
          row.querySelector('progress').value = Math.min(100, Math.max(0, item.progress));
          row.querySelector('[data-stage]').textContent = `${item.stage} · ${item.progress}%`;
          const error = row.querySelector('[data-article-error]'); error.textContent = item.error || ''; error.hidden = !item.error;
          row.querySelector('[data-retry]').hidden = !['failed', 'cancelled'].includes(item.status);
          const log = row.querySelector('[data-process-log]'); log.replaceChildren();
          for (const entry of item.logs) { const li = element('li'); li.append(element('time', entry.time.replace('T', ' ')), element('span', entry.message), element('small', `${entry.progress}%`)); log.append(li); }
          row.querySelector('[data-no-log]').hidden = item.logs.length > 0;
        }
      } catch (error) { failures++; library.querySelector('[data-progress-notice]').textContent = error.message; }
      setTimeout(poll, Math.min(15000, 2500 + failures * 2500));
    };
    setTimeout(poll, 1000);
  }
  const chat = document.querySelector('[data-article-chat]'); if (!chat) return;
  if (matchMedia('(max-width: 760px)').matches) chat.querySelector('.chat-history-toggle').open = false;
  const form = chat.querySelector('[data-chat-form]');
  const messages = chat.querySelector('[data-messages]');
  let running = false, controller = null, historyTimer;
  const status = text => { if (form) form.querySelector('[data-send-status]').textContent = text; };
  const scroll = () => messages.scrollTo({ top: messages.scrollHeight, behavior: 'auto' });
  const downloadLink = id => { const url = new URL(chat.dataset.baseUrl, location.origin); url.searchParams.set('handler', 'Download'); url.searchParams.set('extractionId', id); const link = element('a', '下载 Markdown', 'chat-download'); link.href = url; return link; };
  const refreshHistory = async (refreshTurns = false) => {
    try {
      const url = new URL(chat.dataset.historyUrl, location.origin);
      const conversation = form?.querySelector('[name=Conversation]').value || new URL(location.href).searchParams.get('Conversation');
      if (conversation) url.searchParams.set('Conversation', conversation);
      const response = await fetch(url, { cache: 'no-store', headers: { Accept: 'application/json' } });
      if (!response.ok || !response.headers.get('content-type')?.includes('application/json')) throw new Error('历史记录暂时无法同步，请重新登录或刷新。');
      const data = await response.json();
      const nav = chat.querySelector('[data-conversations]'); nav.replaceChildren();
      chat.querySelector('[data-history-count]').textContent = data.conversations.length;
      chat.querySelector('[data-history-empty]').hidden = data.conversations.length > 0;
      for (const item of data.conversations) {
        const link = element('a', null, item.id === conversation ? 'active' : '');
        const target = new URL(chat.dataset.baseUrl, location.origin); target.searchParams.set('Conversation', item.id); link.href = target;
        link.append(element('strong', item.title), element('small', `${new Date(item.updatedAtUtc).toLocaleString('zh-CN')} · ${item.count} 轮`)); nav.append(link);
      }
      if (refreshTurns && !running && data.turns.length) {
        messages.replaceChildren();
        for (const item of data.turns) {
          const turn = element('article', null, 'chat-turn'); turn.dataset.turnId = item.id; turn.dataset.status = item.status;
          const answer = element('div', null, 'chat-answer markdown-content'); answer.innerHTML = item.html;
          turn.append(element('div', item.prompt, 'chat-user'), answer);
          if (item.status !== 'completed' || item.errorMessage) turn.append(element('p', item.status === 'processing' ? '正在生成，稍后自动同步…' : item.errorMessage || '已停止', 'chat-status'));
          if (item.html) turn.append(downloadLink(item.id));
          messages.append(turn);
        }
      }
      clearTimeout(historyTimer);
      if (!running && data.turns.some(x => x.status === 'processing')) historyTimer = setTimeout(() => refreshHistory(true), 3000);
    } catch (error) { status(error.message); }
  };
  if (messages.querySelector('[data-status="processing"]')) historyTimer = setTimeout(() => refreshHistory(true), 2000);
  chat.querySelectorAll('[data-prompt]').forEach(button => button.addEventListener('click', () => { if (form) { form.querySelector('textarea').value = button.dataset.prompt; form.querySelector('textarea').focus(); } }));
  if (!form) return;
  const textarea = form.querySelector('textarea');
  textarea.addEventListener('keydown', event => { if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) { event.preventDefault(); if (!running) form.requestSubmit(); } });
  chat.addEventListener('click', event => { if (running && event.target.closest('a')) { event.preventDefault(); status('请先等待回复完成，或点击停止生成。'); } });
  form.querySelector('[data-stop]').addEventListener('click', () => controller?.abort());
  form.addEventListener('submit', async event => {
    event.preventDefault(); if (running || !form.reportValidity()) return;
    const prompt = textarea.value.trim(); if (prompt.length < 2) return;
    running = true; controller = new AbortController();
    form.querySelector('[data-send]').hidden = true; form.querySelector('[data-stop]').hidden = false; textarea.readOnly = true;
    chat.querySelector('[data-welcome]')?.remove();
    const turn = element('article', null, 'chat-turn');
    const answer = element('div', '', 'chat-answer markdown-content streaming-answer');
    const notice = element('p', '正在连接模型…', 'chat-status'); notice.setAttribute('role', 'status');
    turn.append(element('div', prompt, 'chat-user'), answer, notice); messages.append(turn); scroll();
    let reply = '', rendered = false, terminal = false;
    const consume = item => {
      const follow = messages.scrollHeight - messages.scrollTop - messages.clientHeight < 100;
      if (item.type === 'start') {
        turn.dataset.turnId = item.id;
        form.querySelector('[name=Conversation]').value = item.conversationId;
        const url = new URL(chat.dataset.baseUrl, location.origin); url.searchParams.set('Conversation', item.conversationId); history.replaceState(null, '', url);
        textarea.value = '';
      }
      if (item.type === 'delta') {
        reply += item.text || '';
        if (item.html) { answer.innerHTML = item.html; answer.classList.remove('streaming-answer'); rendered = true; }
        else if (!rendered) answer.textContent = reply;
        notice.textContent = '正在生成…';
      }
      if (item.type === 'done' || item.type === 'error') {
        terminal = true; answer.innerHTML = item.html || ''; answer.classList.remove('streaming-answer');
        if (item.type === 'done') turn.append(downloadLink(item.id));
        if (item.type === 'error' && !reply && !item.id) textarea.value = prompt;
      }
      if (item.message) notice.textContent = item.message;
      if (follow) scroll();
    };
    try {
      const response = await fetch(form.action, { method: 'POST', body: new FormData(form), signal: controller.signal });
      if (!response.ok || !response.headers.get('content-type')?.includes('application/x-ndjson')) throw new Error('请求未成功，请检查登录状态或稍后重试。');
      const reader = response.body.getReader(), decoder = new TextDecoder(); let buffer = '';
      while (true) {
        const { value, done } = await reader.read(); buffer += decoder.decode(value, { stream: !done });
        const lines = buffer.split('\n'); buffer = lines.pop();
        for (const line of lines) if (line.trim()) consume(JSON.parse(line));
        if (done) { if (buffer.trim()) consume(JSON.parse(buffer)); break; }
      }
      if (!terminal) throw new Error('连接中断，已生成内容会保存在历史中。');
    } catch (error) { notice.textContent = error.name === 'AbortError' ? '已停止生成，正在保存已有内容。' : error.message; if (!turn.dataset.turnId) textarea.value = prompt; }
    finally {
      running = false; controller = null; textarea.readOnly = false; form.querySelector('[data-send]').hidden = false; form.querySelector('[data-stop]').hidden = true;
      await refreshHistory(false);
      status('可继续追问，或从左侧开始新对话。'); textarea.focus();
    }
  });
})();
