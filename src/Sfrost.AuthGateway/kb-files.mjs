import { extname } from "node:path";

export const MAX_KB_FILE_BYTES = 50 * 1024 * 1024;

const MIME_BY_EXTENSION = new Map([
  [".html", "text/html; charset=utf-8"],
  [".htm", "text/html; charset=utf-8"],
  [".md", "text/markdown; charset=utf-8"],
  [".txt", "text/plain; charset=utf-8"],
  [".json", "application/json; charset=utf-8"],
  [".csv", "text/csv; charset=utf-8"],
  [".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"],
  [".doc", "application/msword"],
  [".pdf", "application/pdf"],
  [".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"],
  [".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation"],
]);

export function normalizedExtension(filename) {
  return extname(String(filename)).toLowerCase();
}

export function mimeTypeFor(filename) {
  return MIME_BY_EXTENSION.get(normalizedExtension(filename));
}

export function isSupportedKbFile(filename) {
  return MIME_BY_EXTENSION.has(normalizedExtension(filename));
}

export function isInlineKbMime(mimeType) {
  return mimeType.startsWith("text/") || mimeType === "application/pdf";
}

export function isHtmlKbMime(mimeType) {
  return mimeType.startsWith("text/html");
}
