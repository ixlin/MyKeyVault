using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyKeyVault.Vault.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddControlledUseRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ControlledUseRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    VaultItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RequestedAction = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlledUseRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ControlledUseRequests_OwnerId_Status_CreatedAtUtc",
                table: "ControlledUseRequests",
                columns: new[] { "OwnerId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ControlledUseRequests_VaultItemId_Status",
                table: "ControlledUseRequests",
                columns: new[] { "VaultItemId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ControlledUseRequests");
        }
    }
}
