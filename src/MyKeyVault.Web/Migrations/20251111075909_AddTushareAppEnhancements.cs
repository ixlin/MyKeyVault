using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyKeyVault.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddTushareAppEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CallCount",
                table: "TushareApps",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "TushareApps",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "TushareApps",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "TushareApps",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUsedAt",
                table: "TushareApps",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CallCount",
                table: "TushareApps");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "TushareApps");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "TushareApps");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "TushareApps");

            migrationBuilder.DropColumn(
                name: "LastUsedAt",
                table: "TushareApps");
        }
    }
}
