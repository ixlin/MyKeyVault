(() => {
  const editor = document.getElementById("blog-content");
  if (!editor) return;

  const preview = document.getElementById("blog-preview");
  const previewButton = document.getElementById("editor-preview-toggle");
  const status = document.getElementById("editor-status");
  const uploadInput = document.getElementById("blog-upload-input");
  let uploadMode = "file";
  let uploading = false;

  function announce(message, error = false) {
    status.textContent = message;
    status.classList.toggle("is-error", error);
  }

  function showEditor() {
    preview.hidden = true;
    editor.hidden = false;
    previewButton.setAttribute("aria-pressed", "false");
    previewButton.textContent = "预览";
  }

  function replaceSelection(before, after = before, placeholder = "文字") {
    showEditor();
    const start = editor.selectionStart;
    const end = editor.selectionEnd;
    const selected = editor.value.slice(start, end) || placeholder;
    editor.setRangeText(`${before}${selected}${after}`, start, end, "select");
    editor.setSelectionRange(start + before.length, start + before.length + selected.length);
    editor.focus();
  }

  function prefixLines(prefix) {
    showEditor();
    const start = editor.selectionStart;
    const end = editor.selectionEnd;
    const lineStart = editor.value.lastIndexOf("\n", start - 1) + 1;
    const nextLine = editor.value.indexOf("\n", end);
    const lineEnd = nextLine < 0 ? editor.value.length : nextLine;
    const selected = editor.value.slice(lineStart, lineEnd) || "文字";
    const updated = selected.split("\n").map((line, index) => line.trim()
      ? `${typeof prefix === "function" ? prefix(index) : prefix}${line.replace(/^(?:#{1,6}\s+|>\s+|[-*+]\s+|\d+\.\s+)/, "")}`
      : line).join("\n");
    editor.setRangeText(updated, lineStart, lineEnd, "select");
    editor.focus();
  }

  for (const button of document.querySelectorAll("[data-md]")) {
    button.addEventListener("click", () => {
      const action = button.dataset.md;
      if (action === "bold") replaceSelection("**", "**");
      if (action === "italic") replaceSelection("*", "*");
      if (action === "ul") prefixLines("- ");
      if (action === "ol") prefixLines((index) => `${index + 1}. `);
      if (action === "quote") prefixLines("> ");
      if (action === "link") replaceSelection("[", "](https://example.com)", "链接文字");
    });
  }

  document.getElementById("heading-select").addEventListener("change", (event) => {
    const level = { h1: "# ", h2: "## ", h3: "### " }[event.target.value];
    if (level) prefixLines(level);
    event.target.value = "";
  });

  previewButton.addEventListener("click", async () => {
    if (!preview.hidden) {
      showEditor();
      editor.focus();
      return;
    }
    previewButton.disabled = true;
    announce("正在生成预览…");
    try {
      const response = await fetch("/blog/admin/preview", {
        method: "POST",
        credentials: "same-origin",
        headers: { "Content-Type": "application/x-www-form-urlencoded" },
        body: new URLSearchParams({ content: editor.value }),
      });
      if (!response.ok) throw new Error("预览失败，请检查登录状态后重试。");
      const result = await response.json();
      preview.innerHTML = result.html;
      editor.hidden = true;
      preview.hidden = false;
      previewButton.setAttribute("aria-pressed", "true");
      previewButton.textContent = "继续编辑";
      announce("预览已更新。文章保存后会按此样式展示。");
    } catch (error) {
      announce(error.message, true);
    } finally {
      previewButton.disabled = false;
    }
  });

  document.querySelectorAll("[data-upload]").forEach((button) => {
    button.addEventListener("click", () => {
      if (uploading) return;
      uploadMode = button.dataset.upload;
      uploadInput.accept = uploadMode === "image"
        ? ".png,.jpg,.jpeg,.gif,.webp"
        : ".pdf,.docx,.pptx,.xlsx,.zip,.txt,.md,.csv";
      uploadInput.value = "";
      uploadInput.click();
    });
  });

  uploadInput.addEventListener("change", async () => {
    const file = uploadInput.files?.[0];
    if (!file) return;
    const limit = uploadMode === "image" ? 8 : 50;
    if (file.size > limit * 1024 * 1024) {
      announce(`文件超过 ${limit} MB 上限。`, true);
      return;
    }
    uploading = true;
    announce(`正在上传 ${file.name}…`);
    try {
      const response = await fetch(`/blog/admin/media?name=${encodeURIComponent(file.name)}`, {
        method: "POST",
        credentials: "same-origin",
        headers: { "Content-Type": "application/octet-stream" },
        body: file,
      });
      const result = await response.json();
      if (!response.ok) throw new Error(result.error || "上传失败，请重试。");
      const label = file.name.replace(/[\\[\]()*_`]/g, "\\$&");
      const markup = result.isImage ? `![${label}](${result.url})` : `[${label}](${result.url})`;
      showEditor();
      editor.setRangeText(markup, editor.selectionStart, editor.selectionEnd, "end");
      editor.focus();
      announce(`${file.name} 已插入正文，保存文章后即可查看。`);
    } catch (error) {
      announce(error.message, true);
    } finally {
      uploading = false;
    }
  });

  editor.addEventListener("keydown", (event) => {
    if (!(event.metaKey || event.ctrlKey)) return;
    const key = event.key.toLowerCase();
    if (key === "b" || key === "i") {
      event.preventDefault();
      replaceSelection(key === "b" ? "**" : "*", key === "b" ? "**" : "*");
    }
  });

  editor.closest("form").addEventListener("submit", (event) => {
    if (!uploading) return;
    event.preventDefault();
    announce("请等文件上传完成，再保存文章。", true);
  });
})();
