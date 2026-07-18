using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyKeyVault.Vault.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMcpAccessTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "McpAccessTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Prefix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TokenHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpAccessTokens", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_McpAccessTokens_OwnerId_RevokedAtUtc",
                table: "McpAccessTokens",
                columns: new[] { "OwnerId", "RevokedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_McpAccessTokens_Prefix",
                table: "McpAccessTokens",
                column: "Prefix");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "McpAccessTokens");
        }
    }
}
