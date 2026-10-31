using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaseMVC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveExamTypeCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExamTypes_Code",
                table: "ExamTypes");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "ExamTypes");

            migrationBuilder.CreateIndex(
                name: "IX_ExamTypes_Name",
                table: "ExamTypes",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExamTypes_Name",
                table: "ExamTypes");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "ExamTypes",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ExamTypes_Code",
                table: "ExamTypes",
                column: "Code",
                unique: true);
        }
    }
}
