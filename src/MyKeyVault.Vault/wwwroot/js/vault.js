(() => {
  "use strict";

  const kindSelect = document.querySelector("[data-vault-kind]");
  const accountField = document.querySelector("[data-account-field]");
  const secretField = document.querySelector("[data-secret-field]");
  const notesField = document.querySelector("[data-notes-field]");
  const safetyNote = document.querySelector("[data-card-safety]");
  const cardFields = document.querySelector("[data-card-fields]");

  const updateComposer = () => {
    if (!kindSelect) return;
    const kind = kindSelect.value;
    const isCard = kind === "BankCard" || kind === "CreditCard";
    const isNote = kind === "SecureNote";
    const isApiKey = kind === "ApiKey";

    if (accountField) {
      accountField.hidden = isNote;
      const label = accountField.querySelector("[data-field-label]");
      const input = accountField.querySelector("input");
      if (label) label.textContent = isCard ? "卡号" : "账号信息";
      if (input) {
        input.placeholder = isCard ? "银行卡号或信用卡号" : "用户名、邮箱或登录账号";
        input.autocomplete = isCard ? "off" : "username";
        input.inputMode = isCard ? "numeric" : "text";
      }
    }
    if (secretField) {
      secretField.hidden = isCard || isNote;
      const label = secretField.querySelector("[data-field-label]");
      const input = secretField.querySelector("input");
      if (label) label.textContent = isApiKey ? "API Key / Token" : "密码或密钥";
      if (input) input.placeholder = isApiKey ? "粘贴 API Key 或 Token" : "输入密码或其它私密凭据";
    }
    if (notesField) {
      const label = notesField.querySelector("[data-field-label]");
      if (label) label.textContent = isNote ? "私密笔记" : "备注";
    }
    if (cardFields) cardFields.hidden = !isCard;
    if (safetyNote) safetyNote.hidden = !isCard;
  };

  kindSelect?.addEventListener("change", updateComposer);
  updateComposer();

  const toast = document.querySelector("[data-copy-toast]");
  let toastTimer;
  const showCopyResult = (message, failed = false) => {
    if (!toast) return;
    window.clearTimeout(toastTimer);
    toast.textContent = message;
    toast.hidden = false;
    toast.classList.toggle("is-error", failed);
    requestAnimationFrame(() => toast.classList.add("is-visible"));
    toastTimer = window.setTimeout(() => {
      toast.classList.remove("is-visible");
      window.setTimeout(() => { toast.hidden = true; }, 180);
    }, 1500);
  };

  document.querySelectorAll("[data-copy-target]").forEach((button) => {
    button.addEventListener("click", async () => {
      const target = document.getElementById(button.dataset.copyTarget);
      const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
      if (!target || !token) return;
      try {
        await navigator.clipboard.writeText(target.textContent ?? "");
        const response = await fetch(button.dataset.copyUrl, {
          method: "POST",
          headers: { RequestVerificationToken: token },
          credentials: "same-origin",
        });
        if (!response.ok) throw new Error("audit failed");
        button.classList.add("is-copied");
        showCopyResult("复制成功");
        window.setTimeout(() => button.classList.remove("is-copied"), 1500);
      } catch {
        showCopyResult("复制失败，请重试", true);
      }
    });
  });
})();
