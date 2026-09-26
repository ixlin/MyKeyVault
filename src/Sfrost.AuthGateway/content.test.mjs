import assert from "node:assert/strict";
import { Readable } from "node:stream";
import { mkdtemp, readFile, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";
import { BlogMediaError, saveBlogMediaFile } from "./blog-media.mjs";
import { renderMarkdown } from "./markdown.mjs";

test("Markdown renders reading structure while escaping active HTML", () => {
  const html = renderMarkdown("# 标题\n\n**加粗**、*倾斜*\n\n| A | B |\n|---|---|\n| 1 | 2 |\n\n<script>alert(1)</script>\n\n[bad](javascript:alert(1))");
  assert.match(html, /<h1>标题<\/h1>/);
  assert.match(html, /<strong>加粗<\/strong>/);
  assert.match(html, /<em>倾斜<\/em>/);
  assert.match(html, /<table>/);
  assert.match(html, /&lt;script&gt;/);
  assert.doesNotMatch(html, /<script|href="javascript:/);
});

test("Markdown only embeds approved image URLs", () => {
  const html = renderMarkdown("![本地图](/blog/media/550e8400-e29b-41d4-a716-446655440000) ![外链](http://example.com/a.png)");
  assert.match(html, /<img src="\/blog\/media\//);
  assert.doesNotMatch(html, /src="http:\/\//);
});

test("media upload saves valid images and discards mismatched files", async () => {
  const directory = await mkdtemp(join(tmpdir(), "sfrost-blog-media-test-"));
  try {
    const id = "550e8400-e29b-41d4-a716-446655440000";
    const png = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 1]);
    const request = Readable.from([png]);
    request.headers = { "content-type": "application/octet-stream", "content-length": String(png.length) };
    const result = await saveBlogMediaFile(request, { directory, id, originalName: "示例.png" });
    assert.equal(result.isImage, true);
    assert.deepEqual(await readFile(join(directory, `${id}.png`)), png);

    const badId = "550e8400-e29b-41d4-a716-446655440001";
    const bad = Readable.from([Buffer.from("<script>alert(1)</script>")]);
    bad.headers = { "content-type": "application/octet-stream" };
    await assert.rejects(
      saveBlogMediaFile(bad, { directory, id: badId, originalName: "bad.png" }),
      (error) => error instanceof BlogMediaError && error.status === 415,
    );
    await assert.rejects(readFile(join(directory, `${badId}.png`)), { code: "ENOENT" });
  } finally {
    await rm(directory, { recursive: true, force: true });
  }
});
