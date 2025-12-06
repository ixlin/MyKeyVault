using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyKeyVault.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddWechatArticle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WechatArticles",
                columns: table => new
                {
                    ArticleId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ArticleUniqueId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Author = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PublishTime = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    HtmlFilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ImagesCount = table.Column<int>(type: "integer", nullable: false),
                    VideosCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TaskId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WechatArticles", x => x.ArticleId);
                    table.ForeignKey(
                        name: "FK_WechatArticles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WechatArticles_ArticleUniqueId",
                table: "WechatArticles",
                column: "ArticleUniqueId");

            migrationBuilder.CreateIndex(
                name: "IX_WechatArticles_CreatedAt",
                table: "WechatArticles",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WechatArticles_Status",
                table: "WechatArticles",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WechatArticles_UserId",
                table: "WechatArticles",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WechatArticles");
        }
    }
}
