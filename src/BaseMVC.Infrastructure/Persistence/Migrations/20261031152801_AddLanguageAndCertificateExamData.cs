using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaseMVC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLanguageAndCertificateExamData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnglishIntensiveResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Cohort = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Attendance = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    FinalScore = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Group = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    LastModifiedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ExamId = table.Column<int>(type: "INTEGER", nullable: false),
                    StudentId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnglishIntensiveResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnglishIntensiveResults_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnglishIntensiveResults_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ItBasicCertificateCandidates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IdentityNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CandidateNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Gender = table.Column<bool>(type: "INTEGER", nullable: false),
                    Ethnicity = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    BirthProvince = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ExamDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Session = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ExamDuration = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ExamRoom = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    LastModifiedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ExamId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItBasicCertificateCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItBasicCertificateCandidates_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItBasicCertificateResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IdentityNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CandidateNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Gender = table.Column<bool>(type: "INTEGER", nullable: false),
                    Ethnicity = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    BirthProvince = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    MultipleChoiceScore = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    PracticalScore = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Result = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    LastModifiedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ExamId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItBasicCertificateResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItBasicCertificateResults_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LanguagePlacementCandidates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ExamDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Session = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ExamRoom = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ExamDateSpeaking = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    SessionSpeaking = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ExamRoomSpeaking = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    LastModifiedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ExamId = table.Column<int>(type: "INTEGER", nullable: false),
                    StudentId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LanguagePlacementCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LanguagePlacementCandidates_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LanguagePlacementCandidates_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LanguagePlacementResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Listening = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Reading = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Writing = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Speaking = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    FinalScore = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Result = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    LastModifiedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ExamId = table.Column<int>(type: "INTEGER", nullable: false),
                    StudentId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LanguagePlacementResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LanguagePlacementResults_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LanguagePlacementResults_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LanguageStandardCandidates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RetentionExam = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    RetainedSection = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ExamDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Session = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ExamRoom = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ExamDateSpeaking = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    SessionSpeaking = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ExamRoomSpeaking = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    LastModifiedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ExamId = table.Column<int>(type: "INTEGER", nullable: false),
                    StudentId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LanguageStandardCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LanguageStandardCandidates_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LanguageStandardCandidates_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LanguageStandardResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Listening = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Reading = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Writing = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Speaking = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    FinalScore = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Result = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    LastModifiedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ExamId = table.Column<int>(type: "INTEGER", nullable: false),
                    StudentId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LanguageStandardResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LanguageStandardResults_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LanguageStandardResults_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnglishIntensiveResults_ExamId_StudentId",
                table: "EnglishIntensiveResults",
                columns: new[] { "ExamId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnglishIntensiveResults_StudentId",
                table: "EnglishIntensiveResults",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ItBasicCertificateCandidates_ExamId_CandidateNumber",
                table: "ItBasicCertificateCandidates",
                columns: new[] { "ExamId", "CandidateNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItBasicCertificateCandidates_ExamId_IdentityNumber",
                table: "ItBasicCertificateCandidates",
                columns: new[] { "ExamId", "IdentityNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItBasicCertificateResults_ExamId_CandidateNumber",
                table: "ItBasicCertificateResults",
                columns: new[] { "ExamId", "CandidateNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItBasicCertificateResults_ExamId_IdentityNumber",
                table: "ItBasicCertificateResults",
                columns: new[] { "ExamId", "IdentityNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LanguagePlacementCandidates_ExamId_StudentId",
                table: "LanguagePlacementCandidates",
                columns: new[] { "ExamId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LanguagePlacementCandidates_StudentId",
                table: "LanguagePlacementCandidates",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_LanguagePlacementResults_ExamId_StudentId",
                table: "LanguagePlacementResults",
                columns: new[] { "ExamId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LanguagePlacementResults_StudentId",
                table: "LanguagePlacementResults",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_LanguageStandardCandidates_ExamId_StudentId",
                table: "LanguageStandardCandidates",
                columns: new[] { "ExamId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LanguageStandardCandidates_StudentId",
                table: "LanguageStandardCandidates",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_LanguageStandardResults_ExamId_StudentId",
                table: "LanguageStandardResults",
                columns: new[] { "ExamId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LanguageStandardResults_StudentId",
                table: "LanguageStandardResults",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnglishIntensiveResults");

            migrationBuilder.DropTable(
                name: "ItBasicCertificateCandidates");

            migrationBuilder.DropTable(
                name: "ItBasicCertificateResults");

            migrationBuilder.DropTable(
                name: "LanguagePlacementCandidates");

            migrationBuilder.DropTable(
                name: "LanguagePlacementResults");

            migrationBuilder.DropTable(
                name: "LanguageStandardCandidates");

            migrationBuilder.DropTable(
                name: "LanguageStandardResults");
        }
    }
}
