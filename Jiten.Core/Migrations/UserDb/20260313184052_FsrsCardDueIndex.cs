using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jiten.Core.Migrations.UserDb
{
    /// <inheritdoc />
    public partial class FsrsCardDueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_FsrsCard_UserId_State_Due",
                schema: "user",
                table: "FsrsCards",
                columns: new[] { "UserId", "State", "Due" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FsrsCard_UserId_State_Due",
                schema: "user",
                table: "FsrsCards");
        }
    }
}
