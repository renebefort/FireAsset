using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FireAsset.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddArticleLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArticleLogEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArticleId = table.Column<int>(type: "int", nullable: false),
                    ArticleIdentificationSnapshot = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    UserNameSnapshot = table.Column<string>(type: "nvarchar(201)", maxLength: 201, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleLogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleLogEntries_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArticleLogEntries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleLogEntries_ArticleId",
                table: "ArticleLogEntries",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleLogEntries_Timestamp",
                table: "ArticleLogEntries",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleLogEntries_UserId",
                table: "ArticleLogEntries",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArticleLogEntries");
        }
    }
}
