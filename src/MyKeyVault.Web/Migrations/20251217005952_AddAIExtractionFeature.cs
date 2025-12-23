using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyKeyVault.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddAIExtractionFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AIConfigs",
                columns: table => new
                {
                    ConfigId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ApiKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ModelName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIConfigs", x => x.ConfigId);
                });

            migrationBuilder.CreateTable(
                name: "WechatArticleExtractions",
                columns: table => new
                {
                    ExtractionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ArticleId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Prompt = table.Column<string>(type: "text", nullable: false),
                    Result = table.Column<string>(type: "text", nullable: true),
                    FilePath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ModelUsed = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TokensUsed = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WechatArticleExtractions", x => x.ExtractionId);
                    table.ForeignKey(
                        name: "FK_WechatArticleExtractions_WechatArticles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "WechatArticles",
                        principalColumn: "ArticleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AIConfigs_UserId",
                table: "AIConfigs",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WechatArticleExtractions_ArticleId",
                table: "WechatArticleExtractions",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_WechatArticleExtractions_CreatedAt",
                table: "WechatArticleExtractions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WechatArticleExtractions_UserId",
                table: "WechatArticleExtractions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AIConfigs");

            migrationBuilder.DropTable(
                name: "WechatArticleExtractions");
        }
    }
}
