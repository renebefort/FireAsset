using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FireAsset.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Barcode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    ParentLocationId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Locations_Locations_ParentLocationId",
                        column: x => x.ParentLocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Articles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Identification = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Manufacturer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ManufacturerNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InventoryNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Barcode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AcquisitionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProductionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecommissionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LegalBasis = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsPoolDevice = table.Column<bool>(type: "bit", nullable: false),
                    CurrentInspectionStatus = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Articles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Articles_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Articles_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Articles_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Articles_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FormFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FormVersionId = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FieldType = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    ReferenceValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ShowLastValue = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormFields", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Forms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrentVersionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Forms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FormVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FormId = table.Column<int>(type: "int", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormVersions_Forms_FormId",
                        column: x => x.FormId,
                        principalTable: "Forms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FormVersions_Users_EditedByUserId",
                        column: x => x.EditedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "InspectionIntervals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IntervalMonths = table.Column<int>(type: "int", nullable: false),
                    FormId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionIntervals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionIntervals_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InspectionIntervals_Forms_FormId",
                        column: x => x.FormId,
                        principalTable: "Forms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "InspectionTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArticleId = table.Column<int>(type: "int", nullable: false),
                    IntervalId = table.Column<int>(type: "int", nullable: true),
                    FormId = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsManual = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionTasks_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InspectionTasks_Forms_FormId",
                        column: x => x.FormId,
                        principalTable: "Forms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionTasks_InspectionIntervals_IntervalId",
                        column: x => x.IntervalId,
                        principalTable: "InspectionIntervals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "InspectionProtocols",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArticleId = table.Column<int>(type: "int", nullable: false),
                    TaskId = table.Column<int>(type: "int", nullable: true),
                    FormVersionId = table.Column<int>(type: "int", nullable: false),
                    Result = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsUnplanned = table.Column<bool>(type: "bit", nullable: false),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserName = table.Column<string>(type: "nvarchar(201)", maxLength: 201, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionProtocols", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionProtocols_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionProtocols_FormVersions_FormVersionId",
                        column: x => x.FormVersionId,
                        principalTable: "FormVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionProtocols_InspectionTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "InspectionTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InspectionProtocols_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProtocolFieldValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProtocolId = table.Column<int>(type: "int", nullable: false),
                    FormFieldId = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProtocolFieldValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProtocolFieldValues_FormFields_FormFieldId",
                        column: x => x.FormFieldId,
                        principalTable: "FormFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProtocolFieldValues_InspectionProtocols_ProtocolId",
                        column: x => x.ProtocolId,
                        principalTable: "InspectionProtocols",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Articles_Barcode",
                table: "Articles",
                column: "Barcode",
                unique: true,
                filter: "[Barcode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_CategoryId",
                table: "Articles",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_CreatedByUserId",
                table: "Articles",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_InventoryNumber",
                table: "Articles",
                column: "InventoryNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_LocationId",
                table: "Articles",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_ModifiedByUserId",
                table: "Articles",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormFields_FormVersionId",
                table: "FormFields",
                column: "FormVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Forms_CurrentVersionId",
                table: "Forms",
                column: "CurrentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_FormVersions_EditedByUserId",
                table: "FormVersions",
                column: "EditedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormVersions_FormId_VersionNumber",
                table: "FormVersions",
                columns: new[] { "FormId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionIntervals_CategoryId",
                table: "InspectionIntervals",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionIntervals_FormId",
                table: "InspectionIntervals",
                column: "FormId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionProtocols_ArticleId",
                table: "InspectionProtocols",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionProtocols_CreatedByUserId",
                table: "InspectionProtocols",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionProtocols_FormVersionId",
                table: "InspectionProtocols",
                column: "FormVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionProtocols_TaskId",
                table: "InspectionProtocols",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionTasks_ArticleId",
                table: "InspectionTasks",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionTasks_FormId",
                table: "InspectionTasks",
                column: "FormId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionTasks_IntervalId",
                table: "InspectionTasks",
                column: "IntervalId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionTasks_Status_DueDate",
                table: "InspectionTasks",
                columns: new[] { "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Locations_Barcode",
                table: "Locations",
                column: "Barcode",
                unique: true,
                filter: "[Barcode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_ParentLocationId",
                table: "Locations",
                column: "ParentLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProtocolFieldValues_FormFieldId",
                table: "ProtocolFieldValues",
                column: "FormFieldId");

            migrationBuilder.CreateIndex(
                name: "IX_ProtocolFieldValues_ProtocolId_FormFieldId",
                table: "ProtocolFieldValues",
                columns: new[] { "ProtocolId", "FormFieldId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FormFields_FormVersions_FormVersionId",
                table: "FormFields",
                column: "FormVersionId",
                principalTable: "FormVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Forms_FormVersions_CurrentVersionId",
                table: "Forms",
                column: "CurrentVersionId",
                principalTable: "FormVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FormVersions_Users_EditedByUserId",
                table: "FormVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_Forms_FormVersions_CurrentVersionId",
                table: "Forms");

            migrationBuilder.DropTable(
                name: "ProtocolFieldValues");

            migrationBuilder.DropTable(
                name: "FormFields");

            migrationBuilder.DropTable(
                name: "InspectionProtocols");

            migrationBuilder.DropTable(
                name: "InspectionTasks");

            migrationBuilder.DropTable(
                name: "Articles");

            migrationBuilder.DropTable(
                name: "InspectionIntervals");

            migrationBuilder.DropTable(
                name: "Locations");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "FormVersions");

            migrationBuilder.DropTable(
                name: "Forms");
        }
    }
}
