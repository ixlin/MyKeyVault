using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MyKeyVault.Vault.Data.Migrations;

[DbContext(typeof(VaultDbContext))]
[Migration("20261004000000_ArticleConversationsAndProgress")]
public sealed class ArticleConversationsAndProgress : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>("Progress", "KnowledgeArticles", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<string>("Stage", "KnowledgeArticles", type: "character varying(300)", maxLength: 300, nullable: false, defaultValue: "等待开始");
        migrationBuilder.AddColumn<string>("ProcessLogJson", "KnowledgeArticles", type: "text", nullable: false, defaultValue: "[]");
        migrationBuilder.AddColumn<Guid>("ConversationId", "ArticleExtractions", type: "uuid", nullable: false, defaultValue: Guid.Empty);
        // Historical extractions become individual conversations without changing any content.
        migrationBuilder.Sql("UPDATE \"ArticleExtractions\" SET \"ConversationId\" = md5('mykeyvault-extraction-' || \"Id\"::text)::uuid;");
        migrationBuilder.Sql("UPDATE \"KnowledgeArticles\" SET \"Progress\" = 100, \"Stage\" = '已收录' WHERE \"Status\" = 'completed';");
        migrationBuilder.Sql("UPDATE \"KnowledgeArticles\" SET \"Stage\" = '抓取失败' WHERE \"Status\" = 'failed';");
        migrationBuilder.CreateIndex("IX_ArticleExtractions_OwnerId_ArticleId_ConversationId", "ArticleExtractions", new[] { "OwnerId", "ArticleId", "ConversationId" });
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_ArticleExtractions_OwnerId_ArticleId_ConversationId", "ArticleExtractions");
        migrationBuilder.DropColumn("ConversationId", "ArticleExtractions");
        migrationBuilder.DropColumn("Progress", "KnowledgeArticles");
        migrationBuilder.DropColumn("Stage", "KnowledgeArticles");
        migrationBuilder.DropColumn("ProcessLogJson", "KnowledgeArticles");
    }
}
