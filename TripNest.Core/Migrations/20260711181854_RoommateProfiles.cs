using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripNest.Core.Migrations
{
    /// <inheritdoc />
    public partial class RoommateProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoommateProfiles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Bio = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    University = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    PreferredLocation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MonthlyBudget = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MoveInDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Smokes = table.Column<bool>(type: "boolean", nullable: false),
                    OkWithSmoker = table.Column<bool>(type: "boolean", nullable: false),
                    HasPets = table.Column<bool>(type: "boolean", nullable: false),
                    OkWithPets = table.Column<bool>(type: "boolean", nullable: false),
                    NightOwl = table.Column<bool>(type: "boolean", nullable: false),
                    CleanlinessLevel = table.Column<int>(type: "integer", nullable: false),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoommateProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoommateProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoommateProfiles_IsVisible_PreferredLocation",
                table: "RoommateProfiles",
                columns: new[] { "IsVisible", "PreferredLocation" });

            migrationBuilder.CreateIndex(
                name: "IX_RoommateProfiles_UserId",
                table: "RoommateProfiles",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoommateProfiles");
        }
    }
}
