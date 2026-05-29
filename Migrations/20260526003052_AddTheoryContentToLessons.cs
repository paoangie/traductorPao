using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_TutorIdiomas.Migrations
{
    /// <inheritdoc />
    public partial class AddTheoryContentToLessons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TheoryContent",
                table: "Lessons",
                type: "jsonb",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TheoryContent",
                table: "Lessons");
        }
    }
}
