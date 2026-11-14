using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyFlow.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Availability",
                columns: table => new
                {
                    Day = table.Column<int>(type: "INTEGER", nullable: false),
                    Minutes = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Availability", x => x.Day);
                    table.CheckConstraint("CK_Availability_Day", "Day BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_Availability_Minutes", "Minutes BETWEEN 0 AND 720");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Availability");
        }
    }
}
