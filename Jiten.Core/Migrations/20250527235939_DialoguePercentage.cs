using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jiten.Core.Migrations
{
    /// <inheritdoc />
    public partial class DialoguePercentage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "DialoguePercentage",
                schema: "jiten",
                table: "Decks",
                type: "real",
                nullable: false,
                defaultValue: 0f);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DialoguePercentage",
                schema: "jiten",
                table: "Decks");
        }
    }
}
