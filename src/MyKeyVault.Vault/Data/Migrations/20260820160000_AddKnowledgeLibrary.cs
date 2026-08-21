using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MyKeyVault.Vault.Data;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyKeyVault.Vault.Data.Migrations;

[DbContext(typeof(VaultDbContext))]
[Migration("20260820160000_AddKnowledgeLibrary")]
public sealed class AddKnowledgeLibrary : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ArticleAiSettings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                Provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                BaseUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                ModelName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                ApiKeyCiphertext = table.Column<byte[]>(type: "bytea", nullable: false),
                ApiKeyNonce = table.Column<byte[]>(type: "bytea", nullable: false),
                ApiKeyAuthenticationTag = table.Column<byte[]>(type: "bytea", nullable: false),
                WrappedDataKey = table.Column<byte[]>(type: "bytea", nullable: false),
                KeyWrapNonce = table.Column<byte[]>(type: "bytea", nullable: false),
                KeyWrapAuthenticationTag = table.Column<byte[]>(type: "bytea", nullable: false),
                EncryptionVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ArticleAiSettings", x => x.Id));

        migrationBuilder.CreateTable(
            name: "KnowledgeArticles",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                SourceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                StorageKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                Author = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                PublishedText = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                HtmlFileName = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                PdfFileName = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                ImagesCount = table.Column<int>(type: "integer", nullable: false),
                VideosCount = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                TaskId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                ErrorMessage = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_KnowledgeArticles", x => x.Id));

        migrationBuilder.CreateTable(
            name: "ArticleExtractions",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ArticleId = table.Column<long>(type: "bigint", nullable: false),
                OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                Prompt = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                Result = table.Column<string>(type: "text", nullable: true),
                ModelUsed = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                TokensUsed = table.Column<int>(type: "integer", nullable: true),
                Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                ErrorMessage = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ArticleExtractions", x => x.Id);
                table.ForeignKey("FK_ArticleExtractions_KnowledgeArticles_ArticleId", x => x.ArticleId, "KnowledgeArticles", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_ArticleAiSettings_OwnerId", "ArticleAiSettings", "OwnerId", unique: true);
        migrationBuilder.CreateIndex("IX_ArticleExtractions_ArticleId", "ArticleExtractions", "ArticleId");
        migrationBuilder.CreateIndex("IX_ArticleExtractions_OwnerId_CreatedAtUtc", "ArticleExtractions", new[] { "OwnerId", "CreatedAtUtc" });
        migrationBuilder.CreateIndex("IX_KnowledgeArticles_OwnerId_CreatedAtUtc", "KnowledgeArticles", new[] { "OwnerId", "CreatedAtUtc" });
        migrationBuilder.CreateIndex("IX_KnowledgeArticles_OwnerId_SourceUrl", "KnowledgeArticles", new[] { "OwnerId", "SourceUrl" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("ArticleAiSettings");
        migrationBuilder.DropTable("ArticleExtractions");
        migrationBuilder.DropTable("KnowledgeArticles");
    }
}
