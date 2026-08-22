import http from "node:http";
import { mkdir, readFile, rename, writeFile } from "node:fs/promises";
import {
  createHmac,
  randomBytes,
  scrypt as scryptCallback,
  timingSafeEqual,
} from "node:crypto";
import { dirname } from "node:path";
import { promisify } from "node:util";

const scrypt = promisify(scryptCallback);
const port = parseInteger(process.env.PORT, 3081, 1, 65535);
const credentialsFile = process.env.SFROST_AUTH_CREDENTIALS_FILE || "";
const harnessApiUrl = new URL(
  process.env.SFROST_HARNESS_API_URL || "http://127.0.0.1:3080",
);
if (
  harnessApiUrl.protocol !== "http:"
  || !["127.0.0.1", "::1", "localhost"].includes(harnessApiUrl.hostname)
) {
  throw new Error("SFROST_HARNESS_API_URL must use HTTP on a loopback host.");
}
const initialUsername = requireEnvironment("SFROST_AUTH_USERNAME");
const initialPasswordRecord = requireEnvironment("SFROST_AUTH_PASSWORD_SCRYPT");
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
const DEEPSEEK_CREDENTIAL_REF = "DEEPSEEK_API_KEY";
const failedAttempts = new Map();
const initialCredentials = await loadCredentialState();
let username = initialCredentials.username;
let passwordRecord = parsePasswordRecord(initialCredentials.passwordRecord);
let credentialRevision = initialCredentials.revision;

if (credentialsFile && !initialCredentials.persisted) {
  await persistCredentialState({
    username,
    passwordRecord: formatPasswordRecord(passwordRecord),
    revision: credentialRevision,
  });
}

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

    if (request.method === "GET" && requestUrl.pathname === "/account") {
      if (!verifySession(request)) {
        return redirect(response, "/__sfrost-auth/login");
      }

      return sendAccountPage(response, {
        changed: requestUrl.searchParams.get("changed") === "1",
      });
    }

    if (request.method === "GET" && requestUrl.pathname === "/models") {
      if (!verifySession(request)) {
        return redirect(response, "/__sfrost-auth/login?next=/__sfrost-auth/models");
      }

      const credential = await describeDeepSeekCredential();
      return sendModelsPage(response, {
        credential,
        changed: requestUrl.searchParams.get("changed") === "1",
        removed: requestUrl.searchParams.get("removed") === "1",
      });
    }

    if (request.method === "GET" && requestUrl.pathname === "/models/status") {
      if (!verifySession(request)) return send(response, 401, "Unauthorized");
      const credential = await describeDeepSeekCredential();
      response.setHeader("Cache-Control", "no-store");
      response.setHeader("Content-Type", "application/json; charset=utf-8");
      return send(response, 200, JSON.stringify({ configured: credential.configured }));
    }

    if (request.method === "GET" && requestUrl.pathname === "/models-entry.js") {
      if (!verifySession(request)) return send(response, 401, "Unauthorized");
      response.setHeader("Cache-Control", "no-store");
      response.setHeader("Content-Type", "text/javascript; charset=utf-8");
      response.setHeader("X-Content-Type-Options", "nosniff");
      return send(response, 200, modelsEntryScript());
    }

    if (request.method === "POST" && requestUrl.pathname === "/models/key") {
      if (!verifySession(request)) return send(response, 401, "Unauthorized");
      if (!isSameOrigin(request)) return send(response, 403, "Forbidden");

      const form = await readForm(request);
      const apiKey = form.get("apiKey") ?? "";
      if (
        apiKey.length < 10
        || apiKey.length > 512
        || apiKey.trim() !== apiKey
        || /[\r\n]/.test(apiKey)
      ) {
        return sendModelsPage(response, {
          credential: await describeDeepSeekCredential(),
          error: "Key 格式无效：请输入 10–512 个字符，且首尾不要有空格。",
          status: 400,
        });
      }

      await harnessCredentialRpc("credentials.set", {
        ref: DEEPSEEK_CREDENTIAL_REF,
        value: apiKey,
      });
      return redirect(response, "/__sfrost-auth/models?changed=1");
    }

    if (request.method === "POST" && requestUrl.pathname === "/models/key/delete") {
      if (!verifySession(request)) return send(response, 401, "Unauthorized");
      if (!isSameOrigin(request)) return send(response, 403, "Forbidden");

      const form = await readForm(request);
      if (form.get("confirmation") !== "REMOVE") {
        return sendModelsPage(response, {
          credential: await describeDeepSeekCredential(),
          error: "删除确认无效，请重新操作。",
          status: 400,
        });
      }
      await harnessCredentialRpc("credentials.unset", {
        ref: DEEPSEEK_CREDENTIAL_REF,
      });
      return redirect(response, "/__sfrost-auth/models?removed=1");
    }

    if (request.method === "POST" && requestUrl.pathname === "/account/password") {
      if (!verifySession(request)) {
        return send(response, 401, "Unauthorized");
      }
      if (!isSameOrigin(request)) {
        return send(response, 403, "Forbidden");
      }

      const form = await readForm(request);
      const currentPassword = form.get("currentPassword") ?? "";
      const newPassword = form.get("newPassword") ?? "";
      const confirmPassword = form.get("confirmPassword") ?? "";

      if (!(await verifyPassword(currentPassword))) {
        return sendAccountPage(response, {
          error: "当前密码不正确。",
          status: 400,
        });
      }
      if (newPassword.length < 12 || newPassword.length > 256) {
        return sendAccountPage(response, {
          error: "新密码长度必须为 12–256 个字符。",
          status: 400,
        });
      }
      if (newPassword !== confirmPassword) {
        return sendAccountPage(response, {
          error: "两次输入的新密码不一致。",
          status: 400,
        });
      }
      if (await verifyPassword(newPassword)) {
        return sendAccountPage(response, {
          error: "新密码不能与当前密码相同。",
          status: 400,
        });
      }

      const nextPasswordRecord = await hashPassword(newPassword);
      const nextRevision = credentialRevision + 1;
      await persistCredentialState({
        username,
        passwordRecord: formatPasswordRecord(nextPasswordRecord),
        revision: nextRevision,
      });
      passwordRecord = nextPasswordRecord;
      credentialRevision = nextRevision;

      response.setHeader("Set-Cookie", serializeSessionCookie(SESSION_SECONDS, false));
      return redirect(response, "/__sfrost-auth/account?changed=1");
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

function formatPasswordRecord(record) {
  return `scrypt$${record.salt.toString("hex")}$${record.hash.toString("hex")}`;
}

async function hashPassword(password) {
  const salt = randomBytes(16);
  const hash = await scrypt(password, salt, 64, {
    N: 32768,
    r: 8,
    p: 1,
    maxmem: 64 * 1024 * 1024,
  });
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
    JSON.stringify({
      user: username,
      revision: credentialRevision,
      expires: Date.now() + maxAge * 1000,
    }),
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
    return data.user === username
      && data.revision === credentialRevision
      && Number(data.expires) > Date.now();
  } catch {
    return false;
  }
}

async function loadCredentialState() {
  if (!credentialsFile) {
    return {
      username: initialUsername,
      passwordRecord: initialPasswordRecord,
      revision: 1,
      persisted: false,
    };
  }

  try {
    const document = JSON.parse(await readFile(credentialsFile, "utf8"));
    if (
      document.version !== 1
      || typeof document.username !== "string"
      || typeof document.passwordRecord !== "string"
      || !Number.isInteger(document.revision)
      || document.revision < 1
    ) {
      throw new Error("Credential state has an invalid schema.");
    }
    parsePasswordRecord(document.passwordRecord);
    return { ...document, persisted: true };
  } catch (error) {
    if (error?.code !== "ENOENT") throw error;
    return {
      username: initialUsername,
      passwordRecord: initialPasswordRecord,
      revision: 1,
      persisted: false,
    };
  }
}

async function persistCredentialState(state) {
  if (!credentialsFile) {
    throw new Error("SFROST_AUTH_CREDENTIALS_FILE is required for password changes.");
  }

  await mkdir(dirname(credentialsFile), { recursive: true, mode: 0o700 });
  const temporaryFile = `${credentialsFile}.${process.pid}.${Date.now()}.tmp`;
  const document = `${JSON.stringify({ version: 1, ...state }, null, 2)}\n`;
  await writeFile(temporaryFile, document, { encoding: "utf8", mode: 0o600 });
  await rename(temporaryFile, credentialsFile);
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

  setPageSecurityHeaders(response);
  return send(response, status, loginDocument({ displayUsername, error, next }));
}

function sendAccountPage(response, options = {}) {
  const status = options.status ?? 200;
  const error = options.error
    ? `<p class="notice error" role="alert">${escapeHtml(options.error)}</p>`
    : "";
  const success = options.changed
    ? '<p class="notice success" role="status">密码已更新，其他设备上的旧会话已退出。</p>'
    : "";
  setPageSecurityHeaders(response);
  return send(
    response,
    status,
    accountDocument({ displayUsername: escapeHtml(username), error, success }),
  );
}

function sendModelsPage(response, options = {}) {
  const status = options.status ?? 200;
  const error = options.error
    ? `<p class="notice error" role="alert">${escapeHtml(options.error)}</p>`
    : "";
  const success = options.changed
    ? '<p class="notice success" role="status">DeepSeek API Key 已保存并立即生效。</p>'
    : options.removed
      ? '<p class="notice success" role="status">DeepSeek API Key 已删除。</p>'
      : "";
  setPageSecurityHeaders(response);
  return send(
    response,
    status,
    modelsDocument({
      configured: options.credential.configured,
      writable: options.credential.writable,
      source: options.credential.source,
      error,
      success,
    }),
  );
}

async function describeDeepSeekCredential() {
  const result = await harnessCredentialRpc("credentials.describe", {
    refs: [DEEPSEEK_CREDENTIAL_REF],
  });
  const credential = result.credentials?.[DEEPSEEK_CREDENTIAL_REF];
  if (!credential || typeof credential.configured !== "boolean") {
    throw new Error("Harness returned an invalid credential status.");
  }
  return credential;
}

async function harnessCredentialRpc(method, payload) {
  const rpcId = randomBytes(16).toString("hex");
  const endpoint = new URL(`/api/${method}`, harnessApiUrl);
  const upstream = await fetch(endpoint, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({
      type: "client-request",
      rpcId,
      method,
      payload,
    }),
    signal: AbortSignal.timeout(5_000),
  });
  if (!upstream.ok) {
    throw new Error(`Harness credential service returned HTTP ${upstream.status}.`);
  }
  const document = await upstream.json();
  if (document?.rpcId !== rpcId || document?.result?.ok !== true) {
    const message = document?.result?.error?.message || "credential operation failed";
    throw new Error(`Harness credential service rejected the request: ${message}`);
  }
  return document.result.value ?? {};
}

function setPageSecurityHeaders(response) {
  response.setHeader("Cache-Control", "no-store");
  response.setHeader("Content-Type", "text/html; charset=utf-8");
  response.setHeader(
    "Content-Security-Policy",
    "default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; img-src data:; form-action 'self'; base-uri 'none'; frame-ancestors 'none'",
  );
  response.setHeader("Referrer-Policy", "no-referrer");
  response.setHeader("X-Content-Type-Options", "nosniff");
  response.setHeader("X-Frame-Options", "DENY");
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
    .filing { margin:12px 0 0; font-size:12px; }
    .filing a { color:#70879d; text-decoration:none; }
    .filing a:hover { color:var(--frost); text-decoration:underline; text-underline-offset:3px; }
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
        <p class="filing"><a href="https://beian.miit.gov.cn/" target="_blank" rel="noopener noreferrer">蜀ICP备2024053184号</a></p>
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

function accountDocument({ displayUsername, error, success }) {
  return `<!doctype html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="theme-color" content="#0b1320">
  <title>账户设置 · SFROST</title>
  <style>
    :root { color-scheme:dark; --ink:#0b1320; --panel:#111d2c; --frost:#78d9ff; --blue:#4c78ff; --paper:#f4f8fc; --muted:#91a4b9; --line:rgba(151,211,238,.18); }
    * { box-sizing:border-box; }
    html,body { min-height:100%; }
    body { margin:0; color:var(--paper); background:var(--ink); font-family:-apple-system,BlinkMacSystemFont,"Segoe UI","PingFang SC","Hiragino Sans GB",sans-serif; }
    body::before { content:""; position:fixed; inset:0; pointer-events:none; opacity:.38; background:repeating-radial-gradient(ellipse at 13% 14%,transparent 0 58px,rgba(120,217,255,.08) 59px 60px,transparent 61px 86px); mask-image:linear-gradient(115deg,#000,transparent 66%); }
    .shell { position:relative; width:min(100% - 36px,1060px); margin:0 auto; padding:34px 0 64px; }
    .topbar { display:flex; align-items:center; justify-content:space-between; gap:18px; margin-bottom:clamp(58px,9vw,112px); }
    .brand { display:flex; align-items:center; gap:12px; color:var(--paper); text-decoration:none; font:700 13px/1 ui-monospace,SFMono-Regular,Menlo,monospace; letter-spacing:.18em; }
    .mark { width:28px; aspect-ratio:1; border:1px solid var(--frost); border-radius:50% 50% 44% 44%; display:grid; place-items:center; box-shadow:0 0 28px rgba(120,217,255,.18); }
    .mark::after { content:""; width:5px; height:11px; border-radius:5px 5px 2px 2px; background:var(--frost); box-shadow:0 0 14px var(--frost); }
    .navlinks { display:flex; align-items:center; gap:20px; }
    .back { color:#a9bbcc; text-decoration:none; font-size:14px; }
    .back:hover { color:var(--frost); }
    .layout { display:grid; grid-template-columns:minmax(0,.8fr) minmax(430px,1fr); gap:clamp(48px,10vw,128px); align-items:start; }
    .eyebrow { color:var(--frost); font:600 11px/1 ui-monospace,SFMono-Regular,Menlo,monospace; letter-spacing:.2em; }
    h1 { margin:18px 0; font-family:"Iowan Old Style","Songti SC","Noto Serif SC",serif; font-size:clamp(44px,6vw,76px); font-weight:500; line-height:1; letter-spacing:-.045em; }
    .lead { max-width:28em; margin:0; color:var(--muted); font-size:17px; line-height:1.75; }
    .identity { margin-top:38px; padding-top:20px; border-top:1px solid var(--line); color:#a8bacb; font-size:13px; }
    .identity strong { display:block; margin-top:7px; color:var(--paper); font:650 15px/1.4 ui-monospace,SFMono-Regular,Menlo,monospace; }
    .card { padding:clamp(25px,4vw,42px); border:1px solid var(--line); border-radius:18px; background:rgba(17,29,44,.76); box-shadow:0 28px 80px rgba(0,0,0,.22); backdrop-filter:blur(20px); }
    h2 { margin:0 0 8px; font-size:25px; letter-spacing:-.025em; }
    .hint { margin:0 0 28px; color:var(--muted); font-size:14px; line-height:1.6; }
    .field { margin:0 0 18px; }
    label { display:block; margin-bottom:8px; color:#c7d5e2; font-size:13px; font-weight:650; }
    .control { position:relative; }
    input { width:100%; height:52px; padding:0 48px 0 15px; border:1px solid rgba(157,183,207,.22); border-radius:11px; outline:0; color:var(--paper); background:rgba(5,12,22,.58); font:500 16px/1 inherit; transition:border-color .18s,box-shadow .18s; }
    input:focus { border-color:var(--frost); box-shadow:0 0 0 4px rgba(120,217,255,.1); }
    input[readonly] { color:#9fb2c5; cursor:default; }
    .toggle { position:absolute; right:7px; top:6px; width:40px; height:40px; border:0; border-radius:9px; color:var(--muted); background:transparent; cursor:pointer; font-size:18px; }
    .toggle:hover { color:var(--paper); background:rgba(255,255,255,.06); }
    .submit { width:100%; height:54px; margin-top:6px; border:0; border-radius:11px; color:#fff; background:linear-gradient(105deg,#315fe6,var(--blue) 52%,#4e9df5); box-shadow:0 18px 40px rgba(38,87,225,.22); font-size:15px; font-weight:750; cursor:pointer; }
    .submit:hover { filter:brightness(1.08); }
    .submit:focus-visible,.toggle:focus-visible,.back:focus-visible { outline:2px solid var(--frost); outline-offset:3px; }
    .notice { margin:0 0 20px; padding:12px 14px; border-radius:10px; font-size:14px; line-height:1.55; }
    .error { border:1px solid rgba(255,126,126,.28); color:#ffc0c0; background:rgba(117,31,45,.2); }
    .success { border:1px solid rgba(110,231,183,.25); color:#b8f3dc; background:rgba(23,99,75,.2); }
    .filing { margin:54px 0 0; text-align:center; font-size:12px; }
    .filing a { color:#70879d; text-decoration:none; }
    .filing a:hover { color:var(--frost); text-decoration:underline; text-underline-offset:3px; }
    @media(max-width:780px){.shell{padding-top:24px}.topbar{margin-bottom:54px}.layout{grid-template-columns:1fr;gap:38px}.lead{font-size:16px}.card{padding:24px 20px}.identity{margin-top:26px}}
    @media(prefers-reduced-motion:no-preference){.copy,.card{animation:enter .5s cubic-bezier(.2,.8,.2,1) both}.card{animation-delay:.06s}@keyframes enter{from{opacity:0;transform:translateY(14px)}to{opacity:1;transform:none}}}
  </style>
</head>
<body>
  <main class="shell">
    <nav class="topbar"><a class="brand" href="/"><span class="mark" aria-hidden="true"></span>SFROST / PRIVATE</a><div class="navlinks"><a class="back" href="/__sfrost-auth/models">模型密钥</a><a class="back" href="/">返回工作台 →</a></div></nav>
    <div class="layout">
      <section class="copy">
        <div class="eyebrow">ACCOUNT CONTROL</div>
        <h1>掌管你的通行密钥。</h1>
        <p class="lead">修改后，其他设备上的旧会话会立即失效。当前设备将保持登录。</p>
        <div class="identity">当前账户<strong>${displayUsername}</strong></div>
      </section>
      <section class="card" aria-label="修改密码">
        <h2>修改密码</h2>
        <p class="hint">使用至少 12 个字符，建议包含不同类型的字符。</p>
        ${error}${success}
        <form method="post" action="/__sfrost-auth/account/password">
          <div class="field"><label for="username">用户名</label><div class="control"><input id="username" name="username" type="text" value="${displayUsername}" autocomplete="username" readonly></div></div>
          <div class="field"><label for="currentPassword">当前密码</label><div class="control"><input id="currentPassword" name="currentPassword" type="password" autocomplete="current-password" required autofocus><button class="toggle" type="button" aria-label="显示当前密码">◉</button></div></div>
          <div class="field"><label for="newPassword">新密码</label><div class="control"><input id="newPassword" name="newPassword" type="password" autocomplete="new-password" minlength="12" maxlength="256" required><button class="toggle" type="button" aria-label="显示新密码">◉</button></div></div>
          <div class="field"><label for="confirmPassword">再次输入新密码</label><div class="control"><input id="confirmPassword" name="confirmPassword" type="password" autocomplete="new-password" minlength="12" maxlength="256" required><button class="toggle" type="button" aria-label="显示确认密码">◉</button></div></div>
          <button class="submit" type="submit">保存新密码</button>
        </form>
      </section>
    </div>
    <p class="filing"><a href="https://beian.miit.gov.cn/" target="_blank" rel="noopener noreferrer">蜀ICP备2024053184号</a></p>
  </main>
  <script>
    document.querySelectorAll('.toggle').forEach((button)=>button.addEventListener('click',()=>{const input=button.parentElement.querySelector('input');const visible=input.type==='text';input.type=visible?'password':'text';button.textContent=visible?'◉':'—';button.setAttribute('aria-label',visible?'显示密码':'隐藏密码');input.focus();}));
  </script>
</body>
</html>`;
}

function modelsDocument({ configured, writable, source, error, success }) {
  const stateClass = configured ? "ready" : "missing";
  const stateLabel = configured ? "已配置" : "未配置";
  const sourceLabel = source === "process"
    ? "启动环境（只读）"
    : source === "file"
      ? "Harness 凭据文件"
      : configured
        ? "已配置来源"
        : "尚无凭据";
  const inputDisabled = writable ? "" : " disabled";
  const inputHint = writable
    ? "保存后只显示状态，不会回显 Key。"
    : "当前 Key 来自只读启动环境，需在服务器环境配置中修改。";
  const deleteForm = configured && writable
    ? `<form method="post" action="/__sfrost-auth/models/key/delete" onsubmit="return confirm('确定删除 DeepSeek API Key？删除后模型将无法调用。')"><input type="hidden" name="confirmation" value="REMOVE"><button class="danger" type="submit">删除当前 Key</button></form>`
    : "";

  return `<!doctype html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="theme-color" content="#0b1320">
  <title>模型密钥 · SFROST</title>
  <style>
    :root{color-scheme:dark;--ink:#0b1320;--panel:#111d2c;--frost:#78d9ff;--blue:#4c78ff;--paper:#f4f8fc;--muted:#91a4b9;--line:rgba(151,211,238,.18);--green:#6ee7b7;--red:#ff9999}
    *{box-sizing:border-box}html,body{min-height:100%}body{margin:0;color:var(--paper);background:var(--ink);font-family:-apple-system,BlinkMacSystemFont,"Segoe UI","PingFang SC","Hiragino Sans GB",sans-serif}body::before{content:"";position:fixed;inset:0;pointer-events:none;opacity:.38;background:repeating-radial-gradient(ellipse at 13% 14%,transparent 0 58px,rgba(120,217,255,.08) 59px 60px,transparent 61px 86px);mask-image:linear-gradient(115deg,#000,transparent 66%)}
    .shell{position:relative;width:min(100% - 36px,1060px);margin:0 auto;padding:34px 0 64px}.topbar{display:flex;align-items:center;justify-content:space-between;gap:18px;margin-bottom:clamp(58px,9vw,112px)}.brand{display:flex;align-items:center;gap:12px;color:var(--paper);text-decoration:none;font:700 13px/1 ui-monospace,SFMono-Regular,Menlo,monospace;letter-spacing:.18em}.mark{width:28px;aspect-ratio:1;border:1px solid var(--frost);border-radius:50% 50% 44% 44%;display:grid;place-items:center;box-shadow:0 0 28px rgba(120,217,255,.18)}.mark::after{content:"";width:5px;height:11px;border-radius:5px 5px 2px 2px;background:var(--frost);box-shadow:0 0 14px var(--frost)}.navlinks{display:flex;align-items:center;gap:20px}.back{color:#a9bbcc;text-decoration:none;font-size:14px}.back:hover{color:var(--frost)}
    .layout{display:grid;grid-template-columns:minmax(0,.8fr) minmax(430px,1fr);gap:clamp(48px,10vw,128px);align-items:start}.eyebrow{color:var(--frost);font:600 11px/1 ui-monospace,SFMono-Regular,Menlo,monospace;letter-spacing:.2em}h1{margin:18px 0;font-family:"Iowan Old Style","Songti SC","Noto Serif SC",serif;font-size:clamp(44px,6vw,76px);font-weight:500;line-height:1;letter-spacing:-.045em}.lead{max-width:29em;margin:0;color:var(--muted);font-size:17px;line-height:1.75}.status{display:flex;align-items:center;gap:10px;margin-top:34px;color:#bdccda;font-size:14px}.dot{width:9px;height:9px;border-radius:50%;background:var(--red);box-shadow:0 0 14px rgba(255,153,153,.55)}.status.ready .dot{background:var(--green);box-shadow:0 0 14px rgba(110,231,183,.7)}.source{margin:10px 0 0 19px;color:#72889e;font:12px/1.5 ui-monospace,SFMono-Regular,Menlo,monospace}
    .card{padding:clamp(25px,4vw,42px);border:1px solid var(--line);border-radius:18px;background:rgba(17,29,44,.76);box-shadow:0 28px 80px rgba(0,0,0,.22);backdrop-filter:blur(20px)}h2{margin:0 0 8px;font-size:25px;letter-spacing:-.025em}.hint{margin:0 0 28px;color:var(--muted);font-size:14px;line-height:1.6}.field{margin:0 0 18px}label{display:block;margin-bottom:8px;color:#c7d5e2;font-size:13px;font-weight:650}.control{position:relative}input[type=password]{width:100%;height:52px;padding:0 48px 0 15px;border:1px solid rgba(157,183,207,.22);border-radius:11px;outline:0;color:var(--paper);background:rgba(5,12,22,.58);font:500 16px/1 inherit}input:focus{border-color:var(--frost);box-shadow:0 0 0 4px rgba(120,217,255,.1)}input:disabled{opacity:.5;cursor:not-allowed}.toggle{position:absolute;right:7px;top:6px;width:40px;height:40px;border:0;border-radius:9px;color:var(--muted);background:transparent;cursor:pointer;font-size:18px}.toggle:hover{color:var(--paper);background:rgba(255,255,255,.06)}.submit{width:100%;height:54px;margin-top:6px;border:0;border-radius:11px;color:#fff;background:linear-gradient(105deg,#315fe6,var(--blue) 52%,#4e9df5);box-shadow:0 18px 40px rgba(38,87,225,.22);font-size:15px;font-weight:750;cursor:pointer}.submit:hover{filter:brightness(1.08)}.submit:disabled{opacity:.5;cursor:not-allowed}.formhint{margin:11px 0 0;color:#71869b;font-size:12px;line-height:1.55}.danger{margin-top:22px;padding:0;border:0;color:#d79797;background:transparent;font-size:13px;cursor:pointer}.danger:hover{color:#ffc0c0;text-decoration:underline;text-underline-offset:3px}.notice{margin:0 0 20px;padding:12px 14px;border-radius:10px;font-size:14px;line-height:1.55}.error{border:1px solid rgba(255,126,126,.28);color:#ffc0c0;background:rgba(117,31,45,.2)}.success{border:1px solid rgba(110,231,183,.25);color:#b8f3dc;background:rgba(23,99,75,.2)}.filing{margin:54px 0 0;text-align:center;font-size:12px}.filing a{color:#70879d;text-decoration:none}.filing a:hover{color:var(--frost)}
    @media(max-width:780px){.shell{padding-top:24px}.topbar{margin-bottom:54px}.layout{grid-template-columns:1fr;gap:38px}.lead{font-size:16px}.card{padding:24px 20px}.navlinks{gap:12px}}
  </style>
</head>
<body>
  <main class="shell">
    <nav class="topbar"><a class="brand" href="/"><span class="mark" aria-hidden="true"></span>SFROST / PRIVATE</a><div class="navlinks"><a class="back" href="/__sfrost-auth/account">账户</a><a class="back" href="/">返回工作台 →</a></div></nav>
    <div class="layout">
      <section>
        <div class="eyebrow">MODEL CREDENTIAL</div>
        <h1>连接你的模型。</h1>
        <p class="lead">为 DeepSeek 官方模型保存 API Key。页面只能写入和查看配置状态，任何时候都不会读取或显示 Key 原文。</p>
        <div class="status ${stateClass}"><span class="dot" aria-hidden="true"></span><strong>${stateLabel}</strong></div>
        <p class="source">${escapeHtml(sourceLabel)}</p>
      </section>
      <section class="card" aria-label="DeepSeek API Key 设置">
        <h2>${configured ? "替换 API Key" : "配置 API Key"}</h2>
        <p class="hint">凭据将由 DeepSeek Harness 的本机凭据服务保存。</p>
        ${error}${success}
        <form method="post" action="/__sfrost-auth/models/key">
          <div class="field"><label for="apiKey">DeepSeek API Key</label><div class="control"><input id="apiKey" name="apiKey" type="password" autocomplete="off" minlength="10" maxlength="512" placeholder="sk-…" required${inputDisabled}><button class="toggle" type="button" aria-label="显示 Key"${inputDisabled}>◉</button></div><p class="formhint">${inputHint}</p></div>
          <button class="submit" type="submit"${inputDisabled}>${configured ? "安全替换" : "安全保存"}</button>
        </form>
        ${deleteForm}
      </section>
    </div>
    <p class="filing"><a href="https://beian.miit.gov.cn/" target="_blank" rel="noopener noreferrer">蜀ICP备2024053184号</a></p>
  </main>
  <script>const button=document.querySelector('.toggle');const key=document.querySelector('#apiKey');if(button&&!button.disabled){button.addEventListener('click',()=>{const visible=key.type==='text';key.type=visible?'password':'text';button.textContent=visible?'◉':'—';button.setAttribute('aria-label',visible?'显示 Key':'隐藏 Key');key.focus()})}</script>
</body>
</html>`;
}

function modelsEntryScript() {
  return `(()=>{if(document.getElementById('sfrost-model-key-entry'))return;const css=document.createElement('style');css.textContent='#sfrost-model-key-entry{position:fixed;right:18px;bottom:18px;z-index:2147483000;padding:10px 14px;border:1px solid rgba(120,217,255,.35);border-radius:999px;color:#eaf7ff;background:rgba(11,19,32,.92);box-shadow:0 12px 34px rgba(0,0,0,.32);backdrop-filter:blur(14px);font:600 13px/1.2 -apple-system,BlinkMacSystemFont,"Segoe UI","PingFang SC",sans-serif;text-decoration:none}#sfrost-model-key-entry:hover{border-color:#78d9ff;background:#14243a}#sfrost-key-prompt{position:fixed;inset:0;z-index:2147483001;display:grid;place-items:center;padding:20px;background:rgba(3,8,15,.66);backdrop-filter:blur(8px)}#sfrost-key-prompt>div{width:min(100%,430px);padding:28px;border:1px solid rgba(120,217,255,.24);border-radius:18px;color:#f4f8fc;background:#111d2c;box-shadow:0 28px 90px rgba(0,0,0,.48);font-family:-apple-system,BlinkMacSystemFont,"Segoe UI","PingFang SC",sans-serif}#sfrost-key-prompt h2{margin:0 0 10px;font-size:24px}#sfrost-key-prompt p{margin:0 0 24px;color:#91a4b9;line-height:1.65}#sfrost-key-prompt nav{display:flex;gap:12px}#sfrost-key-prompt a,#sfrost-key-prompt button{height:42px;padding:0 17px;border-radius:9px;font:650 14px/42px inherit;cursor:pointer}#sfrost-key-prompt a{color:white;background:#4c78ff;text-decoration:none}#sfrost-key-prompt button{border:1px solid rgba(255,255,255,.14);color:#b8c7d6;background:transparent}';document.head.append(css);const entry=document.createElement('a');entry.id='sfrost-model-key-entry';entry.href='/__sfrost-auth/models';entry.textContent='模型密钥';document.body.append(entry);fetch('/__sfrost-auth/models/status',{credentials:'same-origin'}).then(r=>r.ok?r.json():null).then(s=>{if(!s||s.configured||sessionStorage.getItem('sfrost-key-prompt-dismissed'))return;const prompt=document.createElement('section');prompt.id='sfrost-key-prompt';prompt.setAttribute('role','dialog');prompt.setAttribute('aria-modal','true');prompt.setAttribute('aria-labelledby','sfrost-key-title');prompt.innerHTML='<div><h2 id="sfrost-key-title">还差一个模型 Key</h2><p>DeepSeek API Key 尚未配置。保存后即可正常发起模型请求。</p><nav><a href="/__sfrost-auth/models">现在配置</a><button type="button">稍后</button></nav></div>';prompt.querySelector('button').onclick=()=>{sessionStorage.setItem('sfrost-key-prompt-dismissed','1');prompt.remove()};document.body.append(prompt)}).catch(()=>{});const reroute=()=>{if(document.body?.textContent?.includes('settings are unavailable in this browser'))location.assign('/__sfrost-auth/models')};let timer;new MutationObserver(()=>{clearTimeout(timer);timer=setTimeout(reroute,100)}).observe(document.body,{childList:true,subtree:true,characterData:true});reroute()})();`;
}
