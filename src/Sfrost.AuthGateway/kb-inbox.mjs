import { constants as fsConstants } from "node:fs";
import {
  chmod,
  copyFile,
  lstat,
  mkdir,
  readFile,
  readdir,
  rename,
  unlink,
} from "node:fs/promises";
import { basename, join } from "node:path";
import { MAX_KB_FILE_BYTES, isSupportedKbFile, mimeTypeFor } from "./kb-files.mjs";
import { createKbDoc, getKbDoc } from "./kb-store.mjs";

const { COPYFILE_EXCL } = fsConstants;

const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
let activeIngest;

export function ingestKbInbox({ inboxDirectory, filesDirectory }) {
  if (activeIngest) return activeIngest;
  activeIngest = runIngest({ inboxDirectory, filesDirectory })
    .finally(() => { activeIngest = undefined; });
  return activeIngest;
}

async function runIngest({ inboxDirectory, filesDirectory }) {
  await mkdir(filesDirectory, { recursive: true, mode: 0o700 });
  let entries;
  try {
    entries = await readdir(inboxDirectory);
  } catch (error) {
    if (error?.code === "ENOENT" || error?.code === "EACCES") return [];
    throw error;
  }

  const results = [];
  for (const manifestName of entries.filter((name) => UUID_PATTERN.test(name.replace(/\.json$/, "")) && name.endsWith(".json")).slice(0, 50)) {
    try {
      results.push(await ingestOne({ inboxDirectory, filesDirectory, manifestName }));
    } catch (error) {
      console.error(`Knowledge-base inbox rejected ${manifestName}:`, error.message);
      await quarantineManifest(inboxDirectory, manifestName);
    }
  }
  return results.filter(Boolean);
}

async function ingestOne({ inboxDirectory, filesDirectory, manifestName }) {
  const manifestPath = join(inboxDirectory, manifestName);
  const manifest = JSON.parse(await readFile(manifestPath, "utf8"));
  validateManifest(manifest, manifestName);

  const existing = await getKbDoc(manifest.id);
  const queuedPath = join(inboxDirectory, manifest.queuedName);
  if (existing) {
    await unlinkIfPresent(queuedPath);
    await unlinkIfPresent(manifestPath);
    return existing;
  }

  const fileInfo = await lstat(queuedPath);
  if (!fileInfo.isFile() || fileInfo.isSymbolicLink()) throw new Error("queued payload is not a regular file");
  if (fileInfo.size !== manifest.sizeBytes || fileInfo.size < 1 || fileInfo.size > MAX_KB_FILE_BYTES) {
    throw new Error("queued payload size does not match manifest");
  }

  const storedName = manifest.queuedName;
  const destination = join(filesDirectory, storedName);
  await copyFile(queuedPath, destination, COPYFILE_EXCL);
  await chmod(destination, 0o600);
  try {
    const document = await createKbDoc({
      id: manifest.id,
      title: manifest.title,
      originalName: manifest.originalName,
      storedName,
      mimeType: manifest.mimeType,
      sizeBytes: manifest.sizeBytes,
      source: "harness",
    });
    await unlinkIfPresent(queuedPath);
    await unlinkIfPresent(manifestPath);
    return document;
  } catch (error) {
    await unlinkIfPresent(destination);
    throw error;
  }
}

function validateManifest(manifest, manifestName) {
  if (manifest?.version !== 1 || !UUID_PATTERN.test(manifest.id ?? "")) {
    throw new Error("invalid manifest identity");
  }
  if (manifestName !== `${manifest.id}.json`) throw new Error("manifest filename does not match id");
  if (typeof manifest.title !== "string" || manifest.title.length < 1 || manifest.title.length > 200 || /[\r\n]/.test(manifest.title)) {
    throw new Error("invalid document title");
  }
  if (typeof manifest.originalName !== "string" || basename(manifest.originalName) !== manifest.originalName || manifest.originalName.length < 1 || manifest.originalName.length > 255 || /[\u0000-\u001f\u007f]/.test(manifest.originalName)) {
    throw new Error("invalid original filename");
  }
  if (!isSupportedKbFile(manifest.originalName)) throw new Error("unsupported document type");
  if (manifest.mimeType !== mimeTypeFor(manifest.originalName)) throw new Error("mime type does not match filename");
  if (manifest.queuedName !== `${manifest.id}${manifest.originalName.slice(manifest.originalName.lastIndexOf(".")).toLowerCase()}`) {
    throw new Error("queued filename does not match manifest");
  }
  if (!Number.isSafeInteger(manifest.sizeBytes)) throw new Error("invalid document size");
}

async function quarantineManifest(directory, manifestName) {
  try {
    await rename(join(directory, manifestName), join(directory, `${manifestName}.rejected`));
  } catch (error) {
    if (error?.code !== "ENOENT") console.error("Failed to quarantine inbox manifest:", error.message);
  }
}

async function unlinkIfPresent(path) {
  try {
    await unlink(path);
  } catch (error) {
    if (error?.code !== "ENOENT") throw error;
  }
}
