using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jiten.Core.Migrations.UserDb
{
    /// <inheritdoc />
    public partial class FsrsIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_FsrsCards_UserId_WordId_ReadingIndex",
                schema: "user",
                table: "FsrsCards",
                columns: new[] { "UserId", "WordId", "ReadingIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FsrsCards_UserId_WordId_ReadingIndex",
                schema: "user",
                table: "FsrsCards");
        }
    }
}
