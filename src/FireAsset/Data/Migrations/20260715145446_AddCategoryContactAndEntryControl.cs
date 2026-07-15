using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FireAsset.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryContactAndEntryControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsEntryControl",
                table: "InspectionIntervals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ContactUserId",
                table: "Categories",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ContactUserId",
                table: "Categories",
                column: "ContactUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Users_ContactUserId",
                table: "Categories",
                column: "ContactUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Users_ContactUserId",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_ContactUserId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "IsEntryControl",
                table: "InspectionIntervals");

            migrationBuilder.DropColumn(
                name: "ContactUserId",
                table: "Categories");
        }
    }
}
