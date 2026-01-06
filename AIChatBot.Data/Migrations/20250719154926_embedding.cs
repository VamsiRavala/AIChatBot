using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIChatBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class embedding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Embedding",
                table: "FAQs",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Embedding",
                table: "FAQs");
        }
    }
}
