using BaseMVC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaseMVC.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261111040000_AddLanguageStandardOnlineSchedule")]
    public partial class AddLanguageStandardOnlineSchedule : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LanguageStandardOnlineSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ExamId = table.Column<int>(type: "INTEGER", nullable: false),
                    StudentId = table.Column<int>(type: "INTEGER", nullable: false),
                    RetainedSection = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    RetentionBatch = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ExamDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ExamRoom = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    TeamsLoginStartTime = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    EExamsStartTime = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ExamDateSpeaking = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ExamRoomSpeaking = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    TeamsLoginStartTimeSpeaking = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    LastModifiedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LanguageStandardOnlineSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LanguageStandardOnlineSchedules_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LanguageStandardOnlineSchedules_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LanguageStandardOnlineSchedules_ExamId_StudentId",
                table: "LanguageStandardOnlineSchedules",
                columns: new[] { "ExamId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LanguageStandardOnlineSchedules_StudentId",
                table: "LanguageStandardOnlineSchedules",
                column: "StudentId");
        }

        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropTable(name: "LanguageStandardOnlineSchedules");
    }
}
