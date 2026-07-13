using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripNest.Core.Migrations
{
    /// <inheritdoc />
    public partial class MaintenanceTriage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TriageCategory",
                table: "Maintenances",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TriageUrgency",
                table: "Maintenances",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TriageCategory",
                table: "Maintenances");

            migrationBuilder.DropColumn(
                name: "TriageUrgency",
                table: "Maintenances");
        }
    }
}
