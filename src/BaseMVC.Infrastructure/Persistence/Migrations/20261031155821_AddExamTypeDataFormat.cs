using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaseMVC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExamTypeDataFormat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DataFormat",
                table: "ExamTypes",
                type: "TEXT",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataFormat",
                table: "ExamTypes");
        }
    }
}
