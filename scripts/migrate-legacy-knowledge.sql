\set ON_ERROR_STOP on

CREATE EXTENSION IF NOT EXISTS dblink;

DO $$
BEGIN
    IF (SELECT count(*) FROM "AspNetUsers") <> 1 THEN
        RAISE EXCEPTION 'Legacy import requires exactly one destination Vault user.';
    END IF;
END $$;

INSERT INTO "KnowledgeArticles" (
    "Id", "OwnerId", "SourceUrl", "StorageKey", "Title", "Author", "PublishedText",
    "HtmlFileName", "PdfFileName", "ImagesCount", "VideosCount", "Status", "TaskId",
    "ErrorMessage", "CreatedAtUtc", "CompletedAtUtc")
SELECT source."ArticleId", destination."Id", source."SourceUrl", source."ArticleUniqueId",
       source."Title", source."Author", source."PublishTime", source."HtmlFilePath", source."PdfPath",
       source."ImagesCount", source."VideosCount", source."Status", source."TaskId",
       left(source."ErrorMessage", 600), source."CreatedAt", source."CompletedAt"
FROM dblink('dbname=mykeyvault', $legacy$
    SELECT "ArticleId", "SourceUrl", "ArticleUniqueId", "Title", "Author", "PublishTime",
           "HtmlFilePath", "PdfPath", "ImagesCount", "VideosCount", "Status", "TaskId",
           "ErrorMessage", "CreatedAt", "CompletedAt"
    FROM "WechatArticles"
    ORDER BY "ArticleId"
$legacy$) AS source(
    "ArticleId" bigint, "SourceUrl" text, "ArticleUniqueId" text, "Title" text, "Author" text,
    "PublishTime" text, "HtmlFilePath" text, "PdfPath" text, "ImagesCount" integer,
    "VideosCount" integer, "Status" text, "TaskId" text, "ErrorMessage" text,
    "CreatedAt" timestamptz, "CompletedAt" timestamptz)
CROSS JOIN "AspNetUsers" destination
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "ArticleExtractions" (
    "Id", "ArticleId", "OwnerId", "Prompt", "Result", "ModelUsed", "TokensUsed",
    "Status", "ErrorMessage", "CreatedAtUtc", "CompletedAtUtc")
SELECT source."ExtractionId", source."ArticleId", destination."Id", left(source."Prompt", 4000),
       source."Result", source."ModelUsed", source."TokensUsed", source."Status",
       left(source."ErrorMessage", 600), source."CreatedAt", source."CompletedAt"
FROM dblink('dbname=mykeyvault', $legacy$
    SELECT "ExtractionId", "ArticleId", "Prompt", "Result", "ModelUsed", "TokensUsed",
           "Status", "ErrorMessage", "CreatedAt", "CompletedAt"
    FROM "WechatArticleExtractions"
    ORDER BY "ExtractionId"
$legacy$) AS source(
    "ExtractionId" bigint, "ArticleId" bigint, "Prompt" text, "Result" text, "ModelUsed" text,
    "TokensUsed" integer, "Status" text, "ErrorMessage" text, "CreatedAt" timestamptz,
    "CompletedAt" timestamptz)
CROSS JOIN "AspNetUsers" destination
WHERE EXISTS (SELECT 1 FROM "KnowledgeArticles" article WHERE article."Id" = source."ArticleId")
ON CONFLICT ("Id") DO NOTHING;

SELECT setval(pg_get_serial_sequence('"KnowledgeArticles"', 'Id'), GREATEST(1, (SELECT coalesce(max("Id"), 1) FROM "KnowledgeArticles")), true);
SELECT setval(pg_get_serial_sequence('"ArticleExtractions"', 'Id'), GREATEST(1, (SELECT coalesce(max("Id"), 1) FROM "ArticleExtractions")), true);
