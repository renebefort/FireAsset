using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FireAsset.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TitleDefault = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RecipientDefault = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SenderDefault = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SubjectDefault = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BodyDefault = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    SignatureDefault = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DefaultTargetLocationId = table.Column<int>(type: "int", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentTemplates_Locations_DefaultTargetLocationId",
                        column: x => x.DefaultTargetLocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TemplateId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Recipient = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Sender = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    Signature = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UsageKind = table.Column<int>(type: "int", nullable: true),
                    UsagePurpose = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UsageDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OrderDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TargetLocationId = table.Column<int>(type: "int", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedByUserId = table.Column<int>(type: "int", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_DocumentTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "DocumentTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Documents_Locations_TargetLocationId",
                        column: x => x.TargetLocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Documents_Users_CompletedByUserId",
                        column: x => x.CompletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Documents_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Documents_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DocumentArticles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    ArticleId = table.Column<int>(type: "int", nullable: true),
                    BarcodeSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IdentificationSnapshot = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CategoryNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentArticles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentArticles_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DocumentArticles_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentArticles_ArticleId",
                table: "DocumentArticles",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentArticles_DocumentId",
                table: "DocumentArticles",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CompletedByUserId",
                table: "Documents",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CreatedByUserId",
                table: "Documents",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ModifiedByUserId",
                table: "Documents",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_TargetLocationId",
                table: "Documents",
                column: "TargetLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_TemplateId",
                table: "Documents",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Type_Status",
                table: "Documents",
                columns: new[] { "Type", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTemplates_DefaultTargetLocationId",
                table: "DocumentTemplates",
                column: "DefaultTargetLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTemplates_Name",
                table: "DocumentTemplates",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentArticles");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "DocumentTemplates");
        }
    }
}
