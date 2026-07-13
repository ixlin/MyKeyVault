using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyKeyVault.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddWechatMiniProgramIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WechatAvatarUrl",
                table: "AspNetUsers",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WechatNickname",
                table: "AspNetUsers",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WechatOpenId",
                table: "AspNetUsers",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_WechatOpenId",
                table: "AspNetUsers",
                column: "WechatOpenId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_WechatOpenId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "WechatAvatarUrl",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "WechatNickname",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "WechatOpenId",
                table: "AspNetUsers");
        }
    }
}
