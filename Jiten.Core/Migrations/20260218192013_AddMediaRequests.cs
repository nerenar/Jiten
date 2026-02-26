using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Jiten.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MediaRequests",
                schema: "jiten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    MediaType = table.Column<int>(type: "integer", nullable: false),
                    ExternalUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExternalLinkType = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AdminNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FulfilledDeckId = table.Column<int>(type: "integer", nullable: true),
                    RequesterId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpvoteCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaRequests_Decks_FulfilledDeckId",
                        column: x => x.FulfilledDeckId,
                        principalSchema: "jiten",
                        principalTable: "Decks",
                        principalColumn: "DeckId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RequestActivityLogs",
                schema: "jiten",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MediaRequestId = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    TargetUserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    Detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestActivityLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaRequestComments",
                schema: "jiten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MediaRequestId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaRequestComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaRequestComments_MediaRequests_MediaRequestId",
                        column: x => x.MediaRequestId,
                        principalSchema: "jiten",
                        principalTable: "MediaRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediaRequestSubscriptions",
                schema: "jiten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MediaRequestId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaRequestSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaRequestSubscriptions_MediaRequests_MediaRequestId",
                        column: x => x.MediaRequestId,
                        principalSchema: "jiten",
                        principalTable: "MediaRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediaRequestUpvotes",
                schema: "jiten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MediaRequestId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaRequestUpvotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaRequestUpvotes_MediaRequests_MediaRequestId",
                        column: x => x.MediaRequestId,
                        principalSchema: "jiten",
                        principalTable: "MediaRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaRequestComment_MediaRequestId",
                schema: "jiten",
                table: "MediaRequestComments",
                column: "MediaRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaRequestComment_UserId",
                schema: "jiten",
                table: "MediaRequestComments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaRequest_MediaType",
                schema: "jiten",
                table: "MediaRequests",
                column: "MediaType");

            migrationBuilder.CreateIndex(
                name: "IX_MediaRequest_RequesterId",
                schema: "jiten",
                table: "MediaRequests",
                column: "RequesterId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaRequest_Status_CreatedAt",
                schema: "jiten",
                table: "MediaRequests",
                columns: new[] { "Status", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_MediaRequest_Status_UpvoteCount",
                schema: "jiten",
                table: "MediaRequests",
                columns: new[] { "Status", "UpvoteCount" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_MediaRequest_Title",
                schema: "jiten",
                table: "MediaRequests",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_MediaRequests_FulfilledDeckId",
                schema: "jiten",
                table: "MediaRequests",
                column: "FulfilledDeckId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaRequestSubscription_RequestId_UserId",
                schema: "jiten",
                table: "MediaRequestSubscriptions",
                columns: new[] { "MediaRequestId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaRequestUpvote_RequestId_UserId",
                schema: "jiten",
                table: "MediaRequestUpvotes",
                columns: new[] { "MediaRequestId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequestActivityLog_Action_CreatedAt",
                schema: "jiten",
                table: "RequestActivityLogs",
                columns: new[] { "Action", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RequestActivityLog_RequestId_CreatedAt",
                schema: "jiten",
                table: "RequestActivityLogs",
                columns: new[] { "MediaRequestId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RequestActivityLog_UserId_CreatedAt",
                schema: "jiten",
                table: "RequestActivityLogs",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaRequestComments",
                schema: "jiten");

            migrationBuilder.DropTable(
                name: "MediaRequestSubscriptions",
                schema: "jiten");

            migrationBuilder.DropTable(
                name: "MediaRequestUpvotes",
                schema: "jiten");

            migrationBuilder.DropTable(
                name: "RequestActivityLogs",
                schema: "jiten");

            migrationBuilder.DropTable(
                name: "MediaRequests",
                schema: "jiten");
        }
    }
}
