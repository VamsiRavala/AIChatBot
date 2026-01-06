using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIChatBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChatSessionEnd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EndedAt",
                table: "ChatVisitors",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndedAt",
                table: "ChatVisitors");
        }
    }
}
