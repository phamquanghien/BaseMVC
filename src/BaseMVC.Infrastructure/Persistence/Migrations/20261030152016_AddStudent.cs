using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaseMVC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Faculties_FacultyCode",
                table: "Faculties",
                column: "FacultyCode");

            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StudentCode = table.Column<string>(type: "TEXT", maxLength: 12, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ClassCode = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FacultyCode = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    Gender = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    EligibleForEnglish = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    PassedLanguageRequirement = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    PassedITRequirement = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    DateOfBirth = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    LastModifiedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Students_Faculties_FacultyCode",
                        column: x => x.FacultyCode,
                        principalTable: "Faculties",
                        principalColumn: "FacultyCode",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Students_FacultyCode",
                table: "Students",
                column: "FacultyCode");

            migrationBuilder.CreateIndex(
                name: "IX_Students_StudentCode",
                table: "Students",
                column: "StudentCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Students");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Faculties_FacultyCode",
                table: "Faculties");
        }
    }
}
