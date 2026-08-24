#!/usr/bin/env node
import { randomUUID } from "node:crypto";
import { constants as fsConstants } from "node:fs";
import {
  chmod,
  copyFile,
  lstat,
  realpath,
  rename,
  unlink,
  writeFile,
} from "node:fs/promises";
import { basename, join, resolve } from "node:path";
import {
  MAX_KB_FILE_BYTES,
  isSupportedKbFile,
  mimeTypeFor,
  normalizedExtension,
} from "./kb-files.mjs";

const { COPYFILE_EXCL } = fsConstants;

const inboxDirectory = process.env.SFROST_KB_INBOX_DIR || "/var/lib/sfrost-kb-inbox";
const publicBaseUrl = process.env.SFROST_PUBLIC_BASE_URL || "https://sfrost.cn";
const allowedSourceRoots = (process.env.SFROST_KB_SOURCE_ROOTS || "/var/lib/deepseek-harness:/srv/sfrost-workspaces")
  .split(":")
  .map((root) => resolve(root))
  .filter(Boolean);

function parseArguments(argv) {
  const positional = [];
  let title = "";
  for (let index = 0; index < argv.length; index += 1) {
    const argument = argv[index];
    if (argument === "--title") {
      title = argv[index + 1] ?? "";
      index += 1;
    } else if (argument.startsWith("--")) {
      throw new Error(`未知参数：${argument}`);
    } else {
      positional.push(argument);
    }
  }
  if (positional.length !== 1) {
    throw new Error("用法：sfrost-publish <文件路径> [--title 标题]");
  }
  return { filePath: positional[0], title };
}

async function main() {
  const options = parseArguments(process.argv.slice(2));
  const absolutePath = resolve(options.filePath);
  const fileInfo = await lstat(absolutePath);
  if (!fileInfo.isFile() || fileInfo.isSymbolicLink()) throw new Error("只能发布普通文件，不能发布符号链接。");
  const resolvedPath = await realpath(absolutePath);
  if (!allowedSourceRoots.some((root) => resolvedPath === root || resolvedPath.startsWith(`${root}/`))) {
    throw new Error("文件不在允许发布的 Harness 工作区内。");
  }
  if (fileInfo.size < 1 || fileInfo.size > MAX_KB_FILE_BYTES) {
    throw new Error("文件大小必须在 1 B–50 MB 之间。");
  }

  const originalName = basename(absolutePath).normalize("NFKC");
  if (originalName.length < 1 || originalName.length > 255 || /[\u0000-\u001f\u007f]/.test(originalName)) {
    throw new Error("文件名必须为 1–255 个字符，且不能包含控制字符。");
  }
  if (!isSupportedKbFile(originalName)) {
    throw new Error("仅支持 HTML、Markdown、TXT、JSON、CSV、Word、PDF、Excel 和 PPTX。 ");
  }
  const title = (options.title || originalName.replace(normalizedExtension(originalName), ""))
    .normalize("NFKC")
    .replace(/[\r\n]/g, " ")
    .trim();
  if (title.length < 1 || title.length > 200) {
    throw new Error("标题必须为 1–200 个字符。");
  }

  const id = randomUUID();
  const extension = normalizedExtension(originalName);
  const queuedName = `${id}${extension}`;
  const temporaryFile = join(inboxDirectory, `.${queuedName}.part`);
  const queuedFile = join(inboxDirectory, queuedName);
  const temporaryManifest = join(inboxDirectory, `.${id}.json.part`);
  const queuedManifest = join(inboxDirectory, `${id}.json`);
  const manifest = {
    version: 1,
    id,
    title,
    originalName,
    queuedName,
    mimeType: mimeTypeFor(originalName),
    sizeBytes: fileInfo.size,
  };

  try {
    await copyFile(resolvedPath, temporaryFile, COPYFILE_EXCL);
    // The inbox directory is the security boundary (2770). copyFile may retain
    // the publisher's primary group instead of the directory's setgid group,
    // so the payload itself must remain readable by the gateway importer.
    await chmod(temporaryFile, 0o644);
    await rename(temporaryFile, queuedFile);
    await writeFile(temporaryManifest, `${JSON.stringify(manifest)}\n`, {
      encoding: "utf8",
      flag: "wx",
      mode: 0o644,
    });
    await rename(temporaryManifest, queuedManifest);
  } catch (error) {
    await Promise.allSettled([temporaryFile, queuedFile, temporaryManifest].map(unlinkIfPresent));
    throw error;
  }

  console.log(`已提交到知识库：${title}`);
  console.log(`访问链接（需登录）：${publicBaseUrl}/kb/d/${id}`);
}

async function unlinkIfPresent(path) {
  try {
    await unlink(path);
  } catch (error) {
    if (error?.code !== "ENOENT") throw error;
  }
}

main().catch((error) => {
  console.error(`发布失败：${error.message}`);
  process.exitCode = 1;
});
