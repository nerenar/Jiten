using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Jiten.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaRequestUploads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MediaRequestUploads",
                schema: "jiten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MediaRequestCommentId = table.Column<int>(type: "integer", nullable: false),
                    MediaRequestId = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    OriginalFileCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AdminReviewed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    AdminNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FileDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaRequestUploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaRequestUploads_MediaRequestComments_MediaRequestCommen~",
                        column: x => x.MediaRequestCommentId,
                        principalSchema: "jiten",
                        principalTable: "MediaRequestComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaRequestUploads_MediaRequests_MediaRequestId",
                        column: x => x.MediaRequestId,
                        principalSchema: "jiten",
                        principalTable: "MediaRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaRequestUpload_CommentId",
                schema: "jiten",
                table: "MediaRequestUploads",
                column: "MediaRequestCommentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaRequestUpload_RequestId",
                schema: "jiten",
                table: "MediaRequestUploads",
                column: "MediaRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaRequestUploads",
                schema: "jiten");
        }
    }
}
