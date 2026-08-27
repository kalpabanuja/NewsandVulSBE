using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewsandVulSBE.Migrations
{
    /// <inheritdoc />
    public partial class AddRawNistJsonToReleasedVulnerabilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RawNistJson",
                table: "ReleasedVulnerabilities",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RawNistJson",
                table: "ReleasedVulnerabilities");
        }
    }
}
