using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jiten.Core.Migrations.UserDb
{
    /// <inheritdoc />
    public partial class Authentication2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "user");

            migrationBuilder.RenameTable(
                name: "RefreshTokens",
                newName: "RefreshTokens",
                newSchema: "user");

            migrationBuilder.RenameTable(
                name: "AspNetUserTokens",
                newName: "AspNetUserTokens",
                newSchema: "user");

            migrationBuilder.RenameTable(
                name: "AspNetUsers",
                newName: "AspNetUsers",
                newSchema: "user");

            migrationBuilder.RenameTable(
                name: "AspNetUserRoles",
                newName: "AspNetUserRoles",
                newSchema: "user");

            migrationBuilder.RenameTable(
                name: "AspNetUserLogins",
                newName: "AspNetUserLogins",
                newSchema: "user");

            migrationBuilder.RenameTable(
                name: "AspNetUserClaims",
                newName: "AspNetUserClaims",
                newSchema: "user");

            migrationBuilder.RenameTable(
                name: "AspNetRoles",
                newName: "AspNetRoles",
                newSchema: "user");

            migrationBuilder.RenameTable(
                name: "AspNetRoleClaims",
                newName: "AspNetRoleClaims",
                newSchema: "user");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "RefreshTokens",
                schema: "user",
                newName: "RefreshTokens");

            migrationBuilder.RenameTable(
                name: "AspNetUserTokens",
                schema: "user",
                newName: "AspNetUserTokens");

            migrationBuilder.RenameTable(
                name: "AspNetUsers",
                schema: "user",
                newName: "AspNetUsers");

            migrationBuilder.RenameTable(
                name: "AspNetUserRoles",
                schema: "user",
                newName: "AspNetUserRoles");

            migrationBuilder.RenameTable(
                name: "AspNetUserLogins",
                schema: "user",
                newName: "AspNetUserLogins");

            migrationBuilder.RenameTable(
                name: "AspNetUserClaims",
                schema: "user",
                newName: "AspNetUserClaims");

            migrationBuilder.RenameTable(
                name: "AspNetRoles",
                schema: "user",
                newName: "AspNetRoles");

            migrationBuilder.RenameTable(
                name: "AspNetRoleClaims",
                schema: "user",
                newName: "AspNetRoleClaims");
        }
    }
}
