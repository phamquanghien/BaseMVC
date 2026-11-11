using BaseMVC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaseMVC.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261111030000_AddItStandardOnlineSchedule")]
    public partial class AddItStandardOnlineSchedule : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ItStandardOnlineSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ExamId = table.Column<int>(type: "INTEGER", nullable: false),
                    StudentId = table.Column<int>(type: "INTEGER", nullable: false),
                    Session = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    TeamsLoginStartTime = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    EExamsLoginStartTime = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    MicrosoftTeamsRoomName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    RetainedSection = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    LastModifiedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItStandardOnlineSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItStandardOnlineSchedules_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItStandardOnlineSchedules_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItStandardOnlineSchedules_ExamId_StudentId",
                table: "ItStandardOnlineSchedules",
                columns: new[] { "ExamId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItStandardOnlineSchedules_StudentId",
                table: "ItStandardOnlineSchedules",
                column: "StudentId");
        }

        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropTable(name: "ItStandardOnlineSchedules");
    }
}
