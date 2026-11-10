using BaseMVC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaseMVC.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261111000000_AddStandardExamResultValidity")]
    public partial class AddStandardExamResultValidity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ValidUntil",
                table: "ItExamResults",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ValidUntil",
                table: "LanguageStandardResults",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ValidUntil", table: "ItExamResults");
            migrationBuilder.DropColumn(name: "ValidUntil", table: "LanguageStandardResults");
        }
    }
}
