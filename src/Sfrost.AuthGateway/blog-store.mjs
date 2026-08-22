import { readFile } from "node:fs/promises";
import pg from "pg";

const { Pool } = pg;
const pool = new Pool({
  host: process.env.SFROST_BLOG_DB_HOST || "/var/run/postgresql",
  database: process.env.SFROST_BLOG_DB_NAME || "sfrost_blog",
  user: process.env.SFROST_BLOG_DB_USER || undefined,
  max: 5,
  idleTimeoutMillis: 30_000,
  connectionTimeoutMillis: 5_000,
  application_name: "sfrost-blog",
});

pool.on("error", (error) => {
  console.error("Unexpected PostgreSQL pool error:", error.message);
});

const postSelect = `
  SELECT p.id, p.title, p.summary, p.content, p.status,
         p.created_at, p.updated_at, p.published_at,
         COALESCE(
           json_agg(json_build_object('id', t.id, 'name', t.name) ORDER BY t.name)
             FILTER (WHERE t.id IS NOT NULL),
           '[]'::json
         ) AS tags
    FROM sfrost_blog_posts p
    LEFT JOIN sfrost_blog_post_tags pt ON pt.post_id = p.id
    LEFT JOIN sfrost_blog_tags t ON t.id = pt.tag_id
`;

export async function initializeBlogStore() {
  const schema = await readFile(new URL("./schema.sql", import.meta.url), "utf8");
  await pool.query(schema);
}

export async function closeBlogStore() {
  await pool.end();
}

export async function listPublishedPosts(tagId) {
  const values = [];
  let where = "WHERE p.status = 'published'";
  if (tagId !== undefined) {
    values.push(tagId);
    where += ` AND EXISTS (
      SELECT 1 FROM sfrost_blog_post_tags selected
       WHERE selected.post_id = p.id AND selected.tag_id = $1
    )`;
  }
  const result = await pool.query(
    `${postSelect} ${where}
     GROUP BY p.id
     ORDER BY p.published_at DESC NULLS LAST, p.updated_at DESC`,
    values,
  );
  return result.rows;
}

export async function getPublishedPost(id) {
  const result = await pool.query(
    `${postSelect}
     WHERE p.id = $1 AND p.status = 'published'
     GROUP BY p.id`,
    [id],
  );
  return result.rows[0];
}

export async function listAllPosts() {
  const result = await pool.query(
    `${postSelect}
     GROUP BY p.id
     ORDER BY p.updated_at DESC`,
  );
  return result.rows;
}

export async function getPost(id) {
  const result = await pool.query(
    `${postSelect}
     WHERE p.id = $1
     GROUP BY p.id`,
    [id],
  );
  return result.rows[0];
}

export async function createPost(post) {
  const client = await pool.connect();
  try {
    await client.query("BEGIN");
    await client.query(
      `INSERT INTO sfrost_blog_posts
       (id, title, summary, content, status, published_at)
       VALUES ($1, $2, $3, $4, $5::varchar,
         CASE WHEN $5::varchar = 'published' THEN now() ELSE NULL END)`,
      [post.id, post.title, post.summary, post.content, post.status],
    );
    await replacePostTags(client, post.id, post.tagIds);
    await client.query("COMMIT");
    return await getPost(post.id);
  } catch (error) {
    await client.query("ROLLBACK");
    throw error;
  } finally {
    client.release();
  }
}

export async function updatePost(id, post) {
  const client = await pool.connect();
  try {
    await client.query("BEGIN");
    const updated = await client.query(
      `UPDATE sfrost_blog_posts
          SET title = $2,
              summary = $3,
              content = $4,
              status = $5::varchar,
              published_at = CASE
                WHEN $5::varchar = 'published' THEN COALESCE(published_at, now())
                ELSE NULL
              END,
              updated_at = now()
        WHERE id = $1
        RETURNING id`,
      [id, post.title, post.summary, post.content, post.status],
    );
    if (updated.rowCount === 0) {
      await client.query("ROLLBACK");
      return undefined;
    }
    await replacePostTags(client, id, post.tagIds);
    await client.query("COMMIT");
    return await getPost(id);
  } catch (error) {
    await client.query("ROLLBACK");
    throw error;
  } finally {
    client.release();
  }
}

export async function deletePost(id) {
  const result = await pool.query(
    "DELETE FROM sfrost_blog_posts WHERE id = $1",
    [id],
  );
  return result.rowCount === 1;
}

async function replacePostTags(client, postId, tagIds) {
  await client.query(
    "DELETE FROM sfrost_blog_post_tags WHERE post_id = $1",
    [postId],
  );
  if (tagIds.length === 0) return;
  await client.query(
    `INSERT INTO sfrost_blog_post_tags (post_id, tag_id)
     SELECT $1, id
       FROM sfrost_blog_tags
      WHERE id = ANY($2::bigint[])`,
    [postId, tagIds],
  );
}

export async function listTags() {
  const result = await pool.query(
    `SELECT t.id, t.name, t.created_at, COUNT(pt.post_id)::integer AS post_count
       FROM sfrost_blog_tags t
       LEFT JOIN sfrost_blog_post_tags pt ON pt.tag_id = t.id
      GROUP BY t.id
      ORDER BY t.name`,
  );
  return result.rows;
}

export async function createTag(name) {
  const normalized = name.normalize("NFKC").trim().toLocaleLowerCase("zh-CN");
  const result = await pool.query(
    `INSERT INTO sfrost_blog_tags (name, normalized_name)
     VALUES ($1, $2)
     ON CONFLICT (normalized_name) DO NOTHING
     RETURNING id, name, created_at`,
    [name.trim(), normalized],
  );
  return result.rows[0];
}

export async function deleteTag(id) {
  const result = await pool.query(
    "DELETE FROM sfrost_blog_tags WHERE id = $1",
    [id],
  );
  return result.rowCount === 1;
}

export async function getBlogStats() {
  const result = await pool.query(
    `SELECT
       COUNT(*)::integer AS total,
       COUNT(*) FILTER (WHERE status = 'published')::integer AS published,
       COUNT(*) FILTER (WHERE status = 'draft')::integer AS drafts
     FROM sfrost_blog_posts`,
  );
  return result.rows[0];
}
