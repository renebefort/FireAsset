using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FireAsset.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReviewFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InspectionProtocols_Articles_ArticleId",
                table: "InspectionProtocols");

            migrationBuilder.DropIndex(
                name: "IX_ProtocolFieldValues_ProtocolId",
                table: "ProtocolFieldValues");

            migrationBuilder.DropIndex(
                name: "IX_Locations_Barcode",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Name",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Articles_Barcode",
                table: "Articles");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 256);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Locations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedDate",
                table: "InspectionProtocols",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserName",
                table: "InspectionProtocols",
                type: "TEXT",
                maxLength: 201,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "InspectionIntervals",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Forms",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Categories",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Articles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Bestandsdaten: Prüfdatum aus dem Erfassungszeitpunkt übernehmen und den
            // Prüfernamen aus der Benutzertabelle snapshotten.
            migrationBuilder.Sql(
                "UPDATE \"InspectionProtocols\" SET \"CompletedDate\" = \"CreatedAt\";");
            migrationBuilder.Sql(
                "UPDATE \"InspectionProtocols\" SET \"CreatedByUserName\" = " +
                "(SELECT \"FirstName\" || ' ' || \"LastName\" FROM \"Users\" WHERE \"Users\".\"Id\" = \"InspectionProtocols\".\"CreatedByUserId\") " +
                "WHERE \"CreatedByUserId\" IS NOT NULL;");

            // Vor den neuen Unique-Indizes: eventuelle Bestands-Duplikate bereinigen.
            migrationBuilder.Sql(
                "DELETE FROM \"ProtocolFieldValues\" WHERE \"Id\" NOT IN " +
                "(SELECT MIN(\"Id\") FROM \"ProtocolFieldValues\" GROUP BY \"ProtocolId\", \"FormFieldId\");");
            migrationBuilder.Sql(
                "UPDATE \"Categories\" SET \"Name\" = \"Name\" || ' (' || \"Id\" || ')' WHERE \"Id\" NOT IN " +
                "(SELECT MIN(\"Id\") FROM \"Categories\" GROUP BY \"Name\");");

            migrationBuilder.CreateIndex(
                name: "IX_ProtocolFieldValues_ProtocolId_FormFieldId",
                table: "ProtocolFieldValues",
                columns: new[] { "ProtocolId", "FormFieldId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Locations_Barcode",
                table: "Locations",
                column: "Barcode",
                unique: true,
                filter: "\"Barcode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Articles_Barcode",
                table: "Articles",
                column: "Barcode",
                unique: true,
                filter: "\"Barcode\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionProtocols_Articles_ArticleId",
                table: "InspectionProtocols",
                column: "ArticleId",
                principalTable: "Articles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InspectionProtocols_Articles_ArticleId",
                table: "InspectionProtocols");

            migrationBuilder.DropIndex(
                name: "IX_ProtocolFieldValues_ProtocolId_FormFieldId",
                table: "ProtocolFieldValues");

            migrationBuilder.DropIndex(
                name: "IX_Locations_Barcode",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Name",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Articles_Barcode",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "CompletedDate",
                table: "InspectionProtocols");

            migrationBuilder.DropColumn(
                name: "CreatedByUserName",
                table: "InspectionProtocols");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "InspectionIntervals");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Forms");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Articles");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 256,
                oldCollation: "NOCASE");

            migrationBuilder.CreateIndex(
                name: "IX_ProtocolFieldValues_ProtocolId",
                table: "ProtocolFieldValues",
                column: "ProtocolId");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_Barcode",
                table: "Locations",
                column: "Barcode",
                unique: true,
                filter: "[Barcode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_Barcode",
                table: "Articles",
                column: "Barcode",
                unique: true,
                filter: "[Barcode] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionProtocols_Articles_ArticleId",
                table: "InspectionProtocols",
                column: "ArticleId",
                principalTable: "Articles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
