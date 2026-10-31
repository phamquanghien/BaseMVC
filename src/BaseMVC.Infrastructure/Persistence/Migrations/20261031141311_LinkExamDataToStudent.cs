using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaseMVC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkExamDataToStudent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StudentId",
                table: "ItExamResults",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StudentId",
                table: "ItExamCandidates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE ItExamCandidates SET StudentId = COALESCE((SELECT Id FROM Students WHERE Students.StudentCode = ItExamCandidates.StudentCode COLLATE NOCASE), 0);
                UPDATE ItExamResults SET StudentId = COALESCE((SELECT Id FROM Students WHERE Students.StudentCode = ItExamResults.StudentCode COLLATE NOCASE), 0);
                DELETE FROM ItExamCandidates WHERE StudentId = 0;
                DELETE FROM ItExamResults WHERE StudentId = 0;
                """);

            migrationBuilder.DropIndex(
                name: "IX_ItExamResults_ExamId_StudentCode",
                table: "ItExamResults");

            migrationBuilder.DropIndex(
                name: "IX_ItExamCandidates_ExamId_StudentCode",
                table: "ItExamCandidates");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "ItExamResults");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "ItExamResults");

            migrationBuilder.DropColumn(
                name: "StudentCode",
                table: "ItExamResults");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "ItExamCandidates");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "ItExamCandidates");

            migrationBuilder.DropColumn(
                name: "StudentCode",
                table: "ItExamCandidates");

            migrationBuilder.CreateIndex(
                name: "IX_ItExamResults_ExamId_StudentId",
                table: "ItExamResults",
                columns: new[] { "ExamId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItExamResults_StudentId",
                table: "ItExamResults",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ItExamCandidates_ExamId_StudentId",
                table: "ItExamCandidates",
                columns: new[] { "ExamId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItExamCandidates_StudentId",
                table: "ItExamCandidates",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ItExamCandidates_Students_StudentId",
                table: "ItExamCandidates",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ItExamResults_Students_StudentId",
                table: "ItExamResults",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ItExamCandidates_Students_StudentId",
                table: "ItExamCandidates");

            migrationBuilder.DropForeignKey(
                name: "FK_ItExamResults_Students_StudentId",
                table: "ItExamResults");

            migrationBuilder.DropIndex(
                name: "IX_ItExamResults_ExamId_StudentId",
                table: "ItExamResults");

            migrationBuilder.DropIndex(
                name: "IX_ItExamResults_StudentId",
                table: "ItExamResults");

            migrationBuilder.DropIndex(
                name: "IX_ItExamCandidates_ExamId_StudentId",
                table: "ItExamCandidates");

            migrationBuilder.DropIndex(
                name: "IX_ItExamCandidates_StudentId",
                table: "ItExamCandidates");

            migrationBuilder.DropColumn(
                name: "StudentId",
                table: "ItExamResults");

            migrationBuilder.DropColumn(
                name: "StudentId",
                table: "ItExamCandidates");

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "ItExamResults",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "ItExamResults",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StudentCode",
                table: "ItExamResults",
                type: "TEXT",
                maxLength: 15,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "ItExamCandidates",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "ItExamCandidates",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StudentCode",
                table: "ItExamCandidates",
                type: "TEXT",
                maxLength: 15,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ItExamResults_ExamId_StudentCode",
                table: "ItExamResults",
                columns: new[] { "ExamId", "StudentCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItExamCandidates_ExamId_StudentCode",
                table: "ItExamCandidates",
                columns: new[] { "ExamId", "StudentCode" },
                unique: true);
        }
    }
}
