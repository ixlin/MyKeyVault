export class HttpInputError extends Error {
  constructor(status, message) {
    super(message);
    this.status = status;
  }
}

export async function readUrlEncodedForm(request, maxBytes) {
  const contentType = String(request.headers["content-type"] ?? "");
  if (contentType.split(";", 1)[0].trim().toLowerCase() !== "application/x-www-form-urlencoded") {
    throw new HttpInputError(415, "Unsupported form content type.");
  }
  const declared = Number(request.headers["content-length"]);
  if (Number.isFinite(declared) && declared > maxBytes) {
    throw new HttpInputError(413, "Form is too large.");
  }

  const chunks = [];
  let size = 0;
  for await (const chunk of request) {
    const bytes = Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk);
    size += bytes.length;
    if (size > maxBytes) throw new HttpInputError(413, "Form is too large.");
    chunks.push(bytes);
  }
  try {
    const text = new TextDecoder("utf-8", { fatal: true }).decode(Buffer.concat(chunks, size));
    return new URLSearchParams(text);
  } catch (error) {
    if (error instanceof TypeError) throw new HttpInputError(400, "Form must use valid UTF-8.");
    throw error;
  }
}
