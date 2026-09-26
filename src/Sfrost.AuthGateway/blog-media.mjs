import { open, mkdir, unlink } from "node:fs/promises";
import { basename, extname, join } from "node:path";

export const MAX_BLOG_MEDIA_BYTES = 50 * 1024 * 1024;
export const MAX_BLOG_IMAGE_BYTES = 8 * 1024 * 1024;
export const MAX_BLOG_MEDIA_TOTAL_BYTES = 512 * 1024 * 1024;

const formats = new Map([
  [".png", ["image/png", "image"]],
  [".jpg", ["image/jpeg", "image"]],
  [".jpeg", ["image/jpeg", "image"]],
  [".gif", ["image/gif", "image"]],
  [".webp", ["image/webp", "image"]],
  [".pdf", ["application/pdf", "pdf"]],
  [".docx", ["application/vnd.openxmlformats-officedocument.wordprocessingml.document", "zip"]],
  [".pptx", ["application/vnd.openxmlformats-officedocument.presentationml.presentation", "zip"]],
  [".xlsx", ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "zip"]],
  [".zip", ["application/zip", "zip"]],
  [".txt", ["text/plain; charset=utf-8", "text"]],
  [".md", ["text/markdown; charset=utf-8", "text"]],
  [".csv", ["text/csv; charset=utf-8", "text"]],
]);

export class BlogMediaError extends Error {
  constructor(status, message) {
    super(message);
    this.status = status;
  }
}

export function blogMediaPath(directory, id, extension) {
  if (!/^[0-9a-f-]{36}$/i.test(id) || !formats.has(extension)) {
    throw new Error("Invalid blog media path.");
  }
  return join(directory, `${id}${extension}`);
}

export async function saveBlogMediaFile(request, { directory, id, originalName }) {
  const name = String(originalName ?? "").normalize("NFC");
  if (!name || name.length > 180 || basename(name) !== name || /[\\/\u0000-\u001f\u007f]/.test(name)) {
    throw new BlogMediaError(400, "文件名无效或超过 180 个字符。");
  }
  const extension = extname(name).toLowerCase();
  const format = formats.get(extension);
  if (!format) throw new BlogMediaError(415, "只支持常见图片、PDF、Office、ZIP 和文本文件。");
  const [mimeType, kind] = format;
  const limit = kind === "image" ? MAX_BLOG_IMAGE_BYTES : MAX_BLOG_MEDIA_BYTES;
  const declared = Number(request.headers["content-length"]);
  if (Number.isFinite(declared) && declared > limit) {
    throw new BlogMediaError(413, `文件超过 ${kind === "image" ? "8" : "50"} MB 上限。`);
  }
  if (request.headers["content-type"] !== "application/octet-stream") {
    throw new BlogMediaError(415, "请通过文章编辑器上传文件。");
  }

  await mkdir(directory, { recursive: true, mode: 0o700 });
  const path = blogMediaPath(directory, id, extension);
  const handle = await open(path, "wx", 0o600);
  let complete = false;
  let sizeBytes = 0;
  let prefix = Buffer.alloc(0);
  try {
    for await (const chunk of request) {
      sizeBytes += chunk.length;
      if (sizeBytes > limit) throw new BlogMediaError(413, `文件超过 ${kind === "image" ? "8" : "50"} MB 上限。`);
      if (prefix.length < 16) prefix = Buffer.concat([prefix, chunk.subarray(0, 16 - prefix.length)]);
      let offset = 0;
      while (offset < chunk.length) {
        const { bytesWritten } = await handle.write(chunk, offset, chunk.length - offset);
        if (bytesWritten === 0) throw new Error("Blog media write made no progress.");
        offset += bytesWritten;
      }
    }
    if (sizeBytes === 0) throw new BlogMediaError(400, "不能上传空文件。");
    if (!hasExpectedSignature(kind, extension, prefix)) {
      throw new BlogMediaError(415, "文件内容与扩展名不一致。");
    }
    complete = true;
    return { id, originalName: name, extension, mimeType, sizeBytes, isImage: kind === "image" };
  } finally {
    await handle.close();
    if (!complete) await unlink(path).catch(() => {});
  }
}

function hasExpectedSignature(kind, extension, bytes) {
  if (kind === "text") return true;
  if (kind === "pdf") return bytes.subarray(0, 5).toString("ascii") === "%PDF-";
  if (kind === "zip") return bytes.subarray(0, 4).equals(Buffer.from([0x50, 0x4b, 0x03, 0x04]));
  if (kind !== "image") return false;
  if (extension === ".png") return bytes.subarray(0, 8).equals(Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]));
  if (extension === ".jpg" || extension === ".jpeg") return bytes.subarray(0, 3).equals(Buffer.from([0xff, 0xd8, 0xff]));
  if (extension === ".gif") return ["GIF87a", "GIF89a"].includes(bytes.subarray(0, 6).toString("ascii"));
  return extension === ".webp" && bytes.subarray(0, 4).toString("ascii") === "RIFF"
    && bytes.subarray(8, 12).toString("ascii") === "WEBP";
}
