using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyKeyVault.Vault.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVaultTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VaultTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaultTags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VaultItemTags",
                columns: table => new
                {
                    TagsId = table.Column<Guid>(type: "uuid", nullable: false),
                    VaultItemsId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaultItemTags", x => new { x.TagsId, x.VaultItemsId });
                    table.ForeignKey(
                        name: "FK_VaultItemTags_VaultItems_VaultItemsId",
                        column: x => x.VaultItemsId,
                        principalTable: "VaultItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VaultItemTags_VaultTags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "VaultTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VaultItemTags_VaultItemsId",
                table: "VaultItemTags",
                column: "VaultItemsId");

            migrationBuilder.CreateIndex(
                name: "IX_VaultTags_OwnerId_Name",
                table: "VaultTags",
                columns: new[] { "OwnerId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VaultItemTags");

            migrationBuilder.DropTable(
                name: "VaultTags");
        }
    }
}
