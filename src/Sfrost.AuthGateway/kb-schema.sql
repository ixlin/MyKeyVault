CREATE TABLE IF NOT EXISTS sfrost_kb_docs (
  id uuid PRIMARY KEY,
  title varchar(200) NOT NULL,
  original_name varchar(255) NOT NULL,
  stored_name varchar(255) NOT NULL UNIQUE,
  mime_type varchar(120) NOT NULL,
  size_bytes bigint NOT NULL CHECK (size_bytes >= 0),
  source varchar(32) NOT NULL DEFAULT 'harness'
    CHECK (source IN ('harness', 'admin')),
  vectorized boolean NOT NULL DEFAULT false,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_sfrost_kb_docs_created
  ON sfrost_kb_docs (created_at DESC);

CREATE INDEX IF NOT EXISTS ix_sfrost_kb_docs_unvectorized
  ON sfrost_kb_docs (created_at DESC)
  WHERE vectorized = false;
