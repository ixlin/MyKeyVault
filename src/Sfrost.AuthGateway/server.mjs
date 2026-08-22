import http from "node:http";
import {
  createHmac,
  scrypt as scryptCallback,
  timingSafeEqual,
} from "node:crypto";
import { promisify } from "node:util";

const scrypt = promisify(scryptCallback);
const port = parseInteger(process.env.PORT, 3081, 1, 65535);
const username = requireEnvironment("SFROST_AUTH_USERNAME");
const passwordRecord = parsePasswordRecord(
  requireEnvironment("SFROST_AUTH_PASSWORD_SCRYPT"),
);
const cookieSecret = Buffer.from(
  requireEnvironment("SFROST_AUTH_COOKIE_SECRET"),
  "hex",
);

if (cookieSecret.length < 32) {
  throw new Error("SFROST_AUTH_COOKIE_SECRET must contain at least 32 bytes of hex.");
}

const COOKIE_NAME = "sfrost_session";
const SESSION_SECONDS = 12 * 60 * 60;
const REMEMBER_SECONDS = 30 * 24 * 60 * 60;
const MAX_FORM_BYTES = 8 * 1024;
const failedAttempts = new Map();

const server = http.createServer(async (request, response) => {
  try {
    const requestUrl = new URL(request.url ?? "/", "http://auth.internal");

    if (request.method === "GET" && requestUrl.pathname === "/health") {
      return send(response, 204);
    }

    // Nginx auth_request subrequests can retain the original HTTP method.
    if (requestUrl.pathname === "/check") {
      return verifySession(request) ? send(response, 204) : send(response, 401);
    }

    if (request.method === "GET" && requestUrl.pathname === "/login") {
      if (verifySession(request)) {
        return redirect(response, safeNext(requestUrl.searchParams.get("next")));
      }

      return sendLoginPage(response, {
        next: safeNext(requestUrl.searchParams.get("next")),
      });
    }

    if (request.method === "POST" && requestUrl.pathname === "/login") {
      if (!isSameOrigin(request)) {
        return send(response, 403, "Forbidden");
      }

      const clientKey = request.headers["x-real-ip"] || request.socket.remoteAddress || "unknown";
      const retryAfter = rateLimitDelay(String(clientKey));
      if (retryAfter > 0) {
        response.setHeader("Retry-After", String(Math.ceil(retryAfter / 1000)));
        return sendLoginPage(response, {
          error: "尝试次数过多，请稍后再试。",
          status: 429,
        });
      }

      const form = await readForm(request);
      const suppliedUsername = form.get("username") ?? "";
      const suppliedPassword = form.get("password") ?? "";
      const validUsername = safeEqualText(suppliedUsername, username);
      const validPassword = await verifyPassword(suppliedPassword);

      if (!validUsername || !validPassword) {
        recordFailure(String(clientKey));
        return sendLoginPage(response, {
          error: "用户名或密码不正确。",
          next: safeNext(form.get("next")),
          status: 401,
        });
      }

      failedAttempts.delete(String(clientKey));
      const remember = form.get("remember") === "on";
      const maxAge = remember ? REMEMBER_SECONDS : SESSION_SECONDS;
      response.setHeader("Set-Cookie", serializeSessionCookie(maxAge, remember));
      return redirect(response, safeNext(form.get("next")));
    }

    if (request.method === "POST" && requestUrl.pathname === "/logout") {
      if (!isSameOrigin(request)) {
        return send(response, 403, "Forbidden");
      }
      response.setHeader(
        "Set-Cookie",
        `${COOKIE_NAME}=; Path=/; HttpOnly; Secure; SameSite=Lax; Max-Age=0`,
      );
      return redirect(response, "/__sfrost-auth/login");
    }

    return send(response, 404, "Not found");
  } catch (error) {
    console.error(error);
    return send(response, 500, "Authentication service unavailable");
  }
});

server.requestTimeout = 10_000;
server.headersTimeout = 12_000;
server.listen(port, "127.0.0.1", () => {
  console.log(`sfrost auth gateway listening on 127.0.0.1:${port}`);
});

function requireEnvironment(name) {
  const value = process.env[name];
  if (!value) throw new Error(`${name} is required.`);
  return value;
}

function parseInteger(value, fallback, minimum, maximum) {
  const parsed = Number.parseInt(value ?? "", 10);
  return Number.isInteger(parsed) && parsed >= minimum && parsed <= maximum
    ? parsed
    : fallback;
}

function parsePasswordRecord(value) {
  const [algorithm, saltHex, hashHex] = value.split("$");
  const salt = Buffer.from(saltHex ?? "", "hex");
  const hash = Buffer.from(hashHex ?? "", "hex");
  if (algorithm !== "scrypt" || salt.length < 16 || hash.length !== 64) {
    throw new Error("SFROST_AUTH_PASSWORD_SCRYPT has an invalid format.");
  }
  return { salt, hash };
}

async function verifyPassword(password) {
  const actual = await scrypt(password, passwordRecord.salt, 64, {
    N: 32768,
    r: 8,
    p: 1,
    maxmem: 64 * 1024 * 1024,
  });
  return timingSafeEqual(actual, passwordRecord.hash);
}

function safeEqualText(left, right) {
  const leftDigest = createHmac("sha256", cookieSecret).update(left).digest();
  const rightDigest = createHmac("sha256", cookieSecret).update(right).digest();
  return timingSafeEqual(leftDigest, rightDigest);
}

function serializeSessionCookie(maxAge, persistent) {
  const payload = Buffer.from(
    JSON.stringify({ user: username, expires: Date.now() + maxAge * 1000 }),
  ).toString("base64url");
  const signature = sign(payload);
  const persistence = persistent ? `; Max-Age=${maxAge}` : "";
  return `${COOKIE_NAME}=${payload}.${signature}; Path=/; HttpOnly; Secure; SameSite=Lax${persistence}`;
}

function verifySession(request) {
  const cookies = parseCookies(request.headers.cookie ?? "");
  const token = cookies.get(COOKIE_NAME);
  if (!token) return false;
  const separator = token.lastIndexOf(".");
  if (separator < 1) return false;

  const payload = token.slice(0, separator);
  const suppliedSignature = token.slice(separator + 1);
  const expectedSignature = sign(payload);
  if (!safeEqualText(suppliedSignature, expectedSignature)) return false;

  try {
    const data = JSON.parse(Buffer.from(payload, "base64url").toString("utf8"));
    return data.user === username && Number(data.expires) > Date.now();
  } catch {
    return false;
  }
}

function sign(payload) {
  return createHmac("sha256", cookieSecret).update(payload).digest("base64url");
}

function parseCookies(header) {
  const cookies = new Map();
  for (const pair of header.split(";")) {
    const separator = pair.indexOf("=");
    if (separator < 1) continue;
    cookies.set(pair.slice(0, separator).trim(), pair.slice(separator + 1).trim());
  }
  return cookies;
}

function safeNext(value) {
  if (!value || !value.startsWith("/") || value.startsWith("//")) return "/";
  if (value.startsWith("/__sfrost-auth/")) return "/";
  return value.replace(/[\r\n]/g, "");
}

function isSameOrigin(request) {
  const origin = request.headers.origin;
  const host = request.headers["x-forwarded-host"] || request.headers.host;
  if (!origin) return true;
  return origin === `https://${host}`;
}

async function readForm(request) {
  const contentType = request.headers["content-type"] ?? "";
  if (!contentType.startsWith("application/x-www-form-urlencoded")) {
    throw new Error("Unsupported form content type.");
  }

  let body = "";
  for await (const chunk of request) {
    body += chunk;
    if (Buffer.byteLength(body) > MAX_FORM_BYTES) {
      throw new Error("Form is too large.");
    }
  }
  return new URLSearchParams(body);
}

function rateLimitDelay(clientKey) {
  const entry = failedAttempts.get(clientKey);
  if (!entry) return 0;
  if (Date.now() - entry.first > 15 * 60 * 1000) {
    failedAttempts.delete(clientKey);
    return 0;
  }
  return entry.count >= 8 ? entry.first + 15 * 60 * 1000 - Date.now() : 0;
}

function recordFailure(clientKey) {
  const entry = failedAttempts.get(clientKey);
  if (!entry || Date.now() - entry.first > 15 * 60 * 1000) {
    failedAttempts.set(clientKey, { count: 1, first: Date.now() });
    return;
  }
  entry.count += 1;
}

function sendLoginPage(response, options = {}) {
  const status = options.status ?? 200;
  const error = options.error
    ? `<p class="error" role="alert">${escapeHtml(options.error)}</p>`
    : "";
  const next = escapeHtml(options.next ?? "/");
  const displayUsername = escapeHtml(username);

  response.setHeader("Cache-Control", "no-store");
  response.setHeader("Content-Type", "text/html; charset=utf-8");
  response.setHeader(
    "Content-Security-Policy",
    "default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; img-src data:; form-action 'self'; base-uri 'none'; frame-ancestors 'none'",
  );
  response.setHeader("Referrer-Policy", "no-referrer");
  response.setHeader("X-Content-Type-Options", "nosniff");
  response.setHeader("X-Frame-Options", "DENY");
  return send(response, status, loginDocument({ displayUsername, error, next }));
}

function send(response, status, body = "") {
  response.statusCode = status;
  response.end(body);
}

function redirect(response, location) {
  response.statusCode = 303;
  response.setHeader("Cache-Control", "no-store");
  response.setHeader("Location", location);
  response.end();
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}

function loginDocument({ displayUsername, error, next }) {
  return `<!doctype html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="theme-color" content="#0b1320">
  <title>进入 SFROST 私有工作台</title>
  <style>
    :root { color-scheme: dark; --ink:#0b1320; --panel:#111d2c; --frost:#78d9ff; --blue:#4c78ff; --paper:#f4f8fc; --muted:#91a4b9; --line:rgba(151,211,238,.18); }
    * { box-sizing: border-box; }
    html, body { min-height: 100%; }
    body { margin: 0; color: var(--paper); background: var(--ink); font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", "PingFang SC", "Hiragino Sans GB", sans-serif; }
    body::before { content:""; position:fixed; inset:0; pointer-events:none; opacity:.42; background: repeating-radial-gradient(ellipse at 13% 14%, transparent 0 58px, rgba(120,217,255,.08) 59px 60px, transparent 61px 86px); mask-image:linear-gradient(115deg,#000,transparent 62%); }
    .shell { min-height:100vh; display:grid; grid-template-columns:minmax(0,1.2fr) minmax(390px,.8fr); }
    .identity { position:relative; display:flex; flex-direction:column; justify-content:space-between; padding:clamp(32px,6vw,88px); overflow:hidden; }
    .brand { display:flex; align-items:center; gap:12px; font:700 13px/1 ui-monospace, SFMono-Regular, Menlo, monospace; letter-spacing:.18em; }
    .mark { width:28px; aspect-ratio:1; border:1px solid var(--frost); border-radius:50% 50% 44% 44%; display:grid; place-items:center; box-shadow:0 0 28px rgba(120,217,255,.18); }
    .mark::after { content:""; width:5px; height:11px; border-radius:5px 5px 2px 2px; background:var(--frost); box-shadow:0 0 14px var(--frost); }
    .intro { max-width:760px; padding:12vh 0 7vh; }
    .eyebrow { color:var(--frost); font:600 12px/1 ui-monospace, SFMono-Regular, Menlo, monospace; letter-spacing:.2em; text-transform:uppercase; }
    h1 { margin:.42em 0 .28em; max-width:9em; font-family:"Iowan Old Style", "Songti SC", "Noto Serif SC", serif; font-size:clamp(52px,8vw,116px); font-weight:500; line-height:.93; letter-spacing:-.045em; }
    .intro p { max-width:31em; margin:0; color:var(--muted); font-size:clamp(16px,1.7vw,20px); line-height:1.75; }
    .status { display:flex; align-items:center; gap:10px; color:#b8c8d8; font:500 12px/1.4 ui-monospace, SFMono-Regular, Menlo, monospace; }
    .status::before { content:""; width:7px; height:7px; border-radius:50%; background:#6ee7b7; box-shadow:0 0 14px rgba(110,231,183,.8); }
    .gate { position:relative; display:grid; place-items:center; padding:32px clamp(24px,5vw,72px); background:rgba(17,29,44,.72); border-left:1px solid var(--line); backdrop-filter:blur(22px); }
    .gate::before { content:""; position:absolute; top:0; left:-1px; width:1px; height:28%; background:linear-gradient(var(--frost),transparent); box-shadow:0 0 18px var(--frost); }
    .card { width:min(100%,430px); }
    .card-head { margin-bottom:34px; }
    .number { color:var(--frost); font:600 11px/1 ui-monospace, SFMono-Regular, Menlo, monospace; letter-spacing:.18em; }
    h2 { margin:13px 0 9px; font-size:30px; letter-spacing:-.03em; }
    .hint { margin:0; color:var(--muted); line-height:1.65; }
    .field { margin:0 0 20px; }
    label { display:block; margin-bottom:9px; color:#c7d5e2; font-size:13px; font-weight:650; }
    .control { position:relative; }
    input[type=text], input[type=password] { width:100%; height:54px; padding:0 48px 0 16px; border:1px solid rgba(157,183,207,.22); border-radius:12px; outline:0; color:var(--paper); background:rgba(5,12,22,.56); font:500 16px/1 inherit; transition:border-color .18s, box-shadow .18s, background .18s; }
    input:focus { border-color:var(--frost); background:rgba(5,12,22,.82); box-shadow:0 0 0 4px rgba(120,217,255,.1); }
    .toggle { position:absolute; right:8px; top:7px; width:40px; height:40px; border:0; border-radius:9px; color:var(--muted); background:transparent; cursor:pointer; font-size:18px; }
    .toggle:hover { color:var(--paper); background:rgba(255,255,255,.06); }
    .remember { display:flex; align-items:center; gap:10px; margin:4px 0 26px; color:#b7c6d5; font-size:14px; cursor:pointer; }
    .remember input { width:17px; height:17px; accent-color:var(--blue); }
    .submit { width:100%; height:56px; border:0; border-radius:12px; color:white; background:linear-gradient(105deg,#315fe6,var(--blue) 52%,#4e9df5); box-shadow:0 18px 40px rgba(38,87,225,.22); font-size:16px; font-weight:750; cursor:pointer; transition:transform .18s, box-shadow .18s; }
    .submit:hover { transform:translateY(-1px); box-shadow:0 22px 48px rgba(38,87,225,.31); }
    .submit:active { transform:translateY(0); }
    .submit:focus-visible, .toggle:focus-visible { outline:2px solid var(--frost); outline-offset:3px; }
    .error { margin:-8px 0 18px; padding:12px 14px; border:1px solid rgba(255,126,126,.28); border-radius:10px; color:#ffc0c0; background:rgba(117,31,45,.2); font-size:14px; }
    .privacy { margin:24px 0 0; color:#6f849a; font-size:12px; line-height:1.6; }
    @media (max-width:820px) { .shell{grid-template-columns:1fr}.identity{min-height:34vh;padding:28px 24px}.intro{padding:8vh 0 1vh}.intro p,.status{display:none}h1{font-size:clamp(44px,14vw,72px)}.gate{border-left:0;border-top:1px solid var(--line);padding:42px 24px 56px}.gate::before{width:32%;height:1px;background:linear-gradient(90deg,var(--frost),transparent)} }
    @media (prefers-reduced-motion:no-preference) { .card{animation:enter .55s cubic-bezier(.2,.8,.2,1) both}.intro{animation:enter .7s .06s cubic-bezier(.2,.8,.2,1) both}@keyframes enter{from{opacity:0;transform:translateY(16px)}to{opacity:1;transform:none}} }
  </style>
</head>
<body>
  <main class="shell">
    <section class="identity" aria-label="SFROST 私有工作台">
      <div class="brand"><span class="mark" aria-hidden="true"></span>SFROST / PRIVATE</div>
      <div class="intro">
        <div class="eyebrow">DeepSeek Harness Portal</div>
        <h1>把工作留在霜线以内。</h1>
        <p>你的私有 AI 工作台。会话、工作区与工具入口仅在身份验证后开放。</p>
      </div>
      <div class="status">TLS 加密连接 · 私有访问</div>
    </section>
    <section class="gate" aria-label="登录">
      <div class="card">
        <header class="card-head">
          <div class="number">ACCESS GATE / 01</div>
          <h2>进入工作台</h2>
          <p class="hint">使用你的 SFROST 访问凭据。</p>
        </header>
        <form method="post" action="/__sfrost-auth/login">
          <input type="hidden" name="next" value="${next}">
          <div class="field">
            <label for="username">用户名</label>
            <div class="control"><input id="username" name="username" type="text" value="${displayUsername}" autocomplete="username" autocapitalize="none" spellcheck="false" required></div>
          </div>
          <div class="field">
            <label for="password">密码</label>
            <div class="control"><input id="password" name="password" type="password" autocomplete="current-password" required autofocus><button class="toggle" type="button" aria-label="显示密码" title="显示密码">◉</button></div>
          </div>
          ${error}
          <label class="remember"><input type="checkbox" name="remember">在此设备保持登录 30 天</label>
          <button class="submit" type="submit">验证并进入</button>
        </form>
        <p class="privacy">凭据只用于本服务器身份验证。浏览器可按你的设置保存密码。</p>
      </div>
    </section>
  </main>
  <script>
    const button=document.querySelector('.toggle');
    const password=document.querySelector('#password');
    button.addEventListener('click',()=>{const visible=password.type==='text';password.type=visible?'password':'text';button.textContent=visible?'◉':'—';button.setAttribute('aria-label',visible?'显示密码':'隐藏密码');password.focus();});
  </script>
</body>
</html>`;
}
