import { readFile } from "node:fs/promises";
import pg from "pg";

const { Pool } = pg;
const pool = new Pool({
  host: process.env.SFROST_BLOG_DB_HOST || "/var/run/postgresql",
  database: process.env.SFROST_BLOG_DB_NAME || "sfrost_blog",
  user: process.env.SFROST_BLOG_DB_USER || undefined,
  max: 3,
  idleTimeoutMillis: 30_000,
  connectionTimeoutMillis: 5_000,
  application_name: "sfrost-kb",
});

pool.on("error", (error) => {
  console.error("Unexpected PostgreSQL knowledge-base pool error:", error.message);
});

export async function initializeKbStore() {
  const schema = await readFile(new URL("./kb-schema.sql", import.meta.url), "utf8");
  await pool.query(schema);
}

export async function listKbDocs(query = "") {
  const normalized = query.normalize("NFKC").trim();
  const values = [];
  let where = "";
  if (normalized) {
    values.push(`%${normalized}%`);
    where = "WHERE title ILIKE $1 OR original_name ILIKE $1";
  }
  const result = await pool.query(
    `SELECT id, title, original_name, stored_name, mime_type, size_bytes,
            source, vectorized, created_at, updated_at
       FROM sfrost_kb_docs
       ${where}
      ORDER BY created_at DESC`,
    values,
  );
  return result.rows;
}

export async function getKbDoc(id) {
  const result = await pool.query(
    "SELECT * FROM sfrost_kb_docs WHERE id = $1",
    [id],
  );
  return result.rows[0];
}

export async function createKbDoc(doc) {
  const result = await pool.query(
    `INSERT INTO sfrost_kb_docs
       (id, title, original_name, stored_name, mime_type, size_bytes, source)
     VALUES ($1, $2, $3, $4, $5, $6, $7)
     RETURNING *`,
    [
      doc.id,
      doc.title,
      doc.originalName,
      doc.storedName,
      doc.mimeType,
      doc.sizeBytes,
      doc.source,
    ],
  );
  return result.rows[0];
}

export async function deleteKbDoc(id) {
  const result = await pool.query(
    "DELETE FROM sfrost_kb_docs WHERE id = $1 RETURNING stored_name",
    [id],
  );
  return result.rows[0]?.stored_name;
}

export async function getKbStats() {
  const result = await pool.query(
    `SELECT COUNT(*)::integer AS total,
            COUNT(*) FILTER (WHERE vectorized = false)::integer AS unvectorized
       FROM sfrost_kb_docs`,
  );
  return result.rows[0];
}
