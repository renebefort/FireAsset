using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FireAsset.Data.Migrations
{
    /// <inheritdoc />
    public partial class IntervalFormOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InspectionIntervals_Forms_FormId",
                table: "InspectionIntervals");

            migrationBuilder.AlterColumn<int>(
                name: "FormId",
                table: "InspectionIntervals",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionIntervals_Forms_FormId",
                table: "InspectionIntervals",
                column: "FormId",
                principalTable: "Forms",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InspectionIntervals_Forms_FormId",
                table: "InspectionIntervals");

            // Intervalle ohne Formular können die wieder verpflichtende FK-Spalte nicht erfüllen
            // (defaultValue 0 wäre eine ungültige Form-Referenz) und müssen beim Rollback entfallen.
            migrationBuilder.Sql("DELETE FROM \"InspectionIntervals\" WHERE \"FormId\" IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "FormId",
                table: "InspectionIntervals",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionIntervals_Forms_FormId",
                table: "InspectionIntervals",
                column: "FormId",
                principalTable: "Forms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
