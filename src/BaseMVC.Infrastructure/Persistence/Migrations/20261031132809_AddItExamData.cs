using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaseMVC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddItExamData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ItExamCandidates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ExamId = table.Column<int>(type: "INTEGER", nullable: false),
                    SequenceNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    StudentCode = table.Column<string>(type: "TEXT", maxLength: 15, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ExamDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Session = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ExamDuration = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ExamRoom = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    RetentionNote = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    RetainedSection = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    LastModifiedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItExamCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItExamCandidates_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItExamResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ExamId = table.Column<int>(type: "INTEGER", nullable: false),
                    SequenceNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    StudentCode = table.Column<string>(type: "TEXT", maxLength: 15, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MultipleChoiceScore = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    PracticalScore = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    FinalScore = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    LastModifiedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItExamResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItExamResults_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItExamCandidates_ExamId_StudentCode",
                table: "ItExamCandidates",
                columns: new[] { "ExamId", "StudentCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItExamResults_ExamId_StudentCode",
                table: "ItExamResults",
                columns: new[] { "ExamId", "StudentCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItExamCandidates");

            migrationBuilder.DropTable(
                name: "ItExamResults");
        }
    }
}
