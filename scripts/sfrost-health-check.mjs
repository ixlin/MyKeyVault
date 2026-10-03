#!/usr/bin/env node
// Run as root on the host. Credentials stay in memory; no password or Key is printed.
import assert from "node:assert/strict";
import { createHmac } from "node:crypto";
import { readFile } from "node:fs/promises";
import https from "node:https";

const environment = await readFile("/etc/sfrost-auth-gateway.env", "utf8");
const secret = environment.match(/^SFROST_AUTH_COOKIE_SECRET=['"]?([0-9a-f]+)['"]?\s*$/im)?.[1];
assert.ok(secret?.length >= 64, "Gateway signing secret is missing");
const state = JSON.parse(await readFile("/var/lib/sfrost-auth-gateway/credentials.json", "utf8"));
const payload = Buffer.from(JSON.stringify({
  user: state.username,
  revision: state.revision,
  expires: Date.now() + 120_000,
})).toString("base64url");
const signature = createHmac("sha256", Buffer.from(secret, "hex")).update(payload).digest("base64url");
const portalCookie = `sfrost_session=${payload}.${signature}`;

function request(path, { cookie, method = "GET", body, contentType } = {}) {
  return new Promise((resolve, reject) => {
    const req = https.request({
      hostname: "127.0.0.1",
      servername: "sfrost.cn",
      port: 443,
      path,
      method,
      headers: {
        host: "sfrost.cn",
        ...(cookie ? { cookie } : {}),
        ...(method !== "GET" ? { origin: "https://sfrost.cn" } : {}),
        ...(body !== undefined ? {
          "content-type": contentType ?? "application/x-www-form-urlencoded",
          "content-length": Buffer.byteLength(body),
        } : {}),
      },
      timeout: 15_000,
    }, (res) => {
      const chunks = [];
      res.on("data", (chunk) => chunks.push(chunk));
      res.on("end", () => resolve({ status: res.statusCode, headers: res.headers, body: Buffer.concat(chunks).toString("utf8") }));
      res.on("error", reject);
    });
    req.on("timeout", () => req.destroy(new Error(`Timed out: ${path}`)));
    req.on("error", reject);
    req.end(body);
  });
}

for (const path of ["/", "/kb", "/blog", "/blog/admin", "/blog/account", "/harness"]) {
  const result = await request(path);
  assert.equal(result.status, 303, `Login required for ${path}`);
  console.log(`PASS login required: ${path}`);
}
for (const path of ["/", "/kb", "/blog", "/blog/admin", "/blog/admin/posts/new", "/blog/admin/media", "/blog/account", "/__sfrost-auth/account", "/__sfrost-auth/models"]) {
  const result = await request(path, { cookie: portalCookie });
  assert.equal(result.status, 200, `Authenticated page ${path}`);
  console.log(`PASS authenticated page: ${path}`);
}
const status = await request("/__sfrost-auth/models/status", { cookie: portalCookie });
assert.equal(status.status, 200, "Model credential bridge");
assert.equal(typeof JSON.parse(status.body).configured, "boolean", "Credential status has no Key value");
console.log("PASS model credential bridge");

const preview = await request("/blog/admin/preview", {
  cookie: portalCookie, method: "POST",
  body: new URLSearchParams({ content: "# 中文标题\n\n**加粗**\n\n<script>alert(1)</script>" }).toString(),
});
assert.equal(preview.status, 200, "Markdown preview");
const html = JSON.parse(preview.body).html;
assert.match(html, /<h1>中文标题<\/h1>/);
assert.match(html, /<strong>加粗<\/strong>/);
assert.doesNotMatch(html, /<script>/);
console.log("PASS Markdown preview and HTML escaping");

const invalid = await request("/blog/admin/preview", { cookie: portalCookie, method: "POST", body: "{}", contentType: "application/json" });
assert.equal(invalid.status, 415, "Malformed forms must not produce a service failure");
console.log("PASS invalid form reports HTTP 415");

let harness = await request("/harness", { cookie: portalCookie });
let harnessCookie = portalCookie;
for (let attempt = 0; harness.status === 303 && attempt < 3; attempt += 1) {
  assert.equal(harness.headers.location, "/harness", "Harness bootstrap redirects locally");
  const issued = harness.headers["set-cookie"]?.map((value) => value.split(";", 1)[0]) ?? [];
  harnessCookie = [harnessCookie, ...issued].join("; ");
  harness = await request("/harness", { cookie: harnessCookie });
}
assert.equal(harness.status, 200, "Harness browser bootstrap");
assert.match(harness.body, /Ben熊的AI Space/);
assert.match(harness.body, /models-entry\.js/);
console.log("PASS Harness authenticated browser bootstrap");
console.log("All sfrost.cn checks passed; no content was created or deleted.");
