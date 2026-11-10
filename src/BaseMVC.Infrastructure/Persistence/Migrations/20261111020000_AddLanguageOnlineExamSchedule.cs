using BaseMVC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaseMVC.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261111020000_AddLanguageOnlineExamSchedule")]
    public partial class AddLanguageOnlineExamSchedule : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LanguageOnlineExamSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ExamId = table.Column<int>(type: "INTEGER", nullable: false),
                    StudentId = table.Column<int>(type: "INTEGER", nullable: false),
                    MockExamDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    MockExamRoom = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    TeamsLoginStartTime = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SebLoginStartTime = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    RetainedSection = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    RetentionBatch = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    LastModifiedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LanguageOnlineExamSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LanguageOnlineExamSchedules_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LanguageOnlineExamSchedules_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LanguageOnlineExamSchedules_ExamId_StudentId",
                table: "LanguageOnlineExamSchedules",
                columns: new[] { "ExamId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LanguageOnlineExamSchedules_StudentId",
                table: "LanguageOnlineExamSchedules",
                column: "StudentId");
        }

        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropTable(name: "LanguageOnlineExamSchedules");
    }
}
