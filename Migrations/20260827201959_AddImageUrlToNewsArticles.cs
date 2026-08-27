using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewsandVulSBE.Migrations
{
    /// <inheritdoc />
    public partial class AddImageUrlToNewsArticles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "NewsArticles",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "NewsArticles");
        }
    }
}
