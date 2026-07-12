using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FireAsset.Data.Migrations
{
    /// <inheritdoc />
    public partial class PoolDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPoolDevice",
                table: "Articles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPoolDevice",
                table: "Articles");
        }
    }
}
