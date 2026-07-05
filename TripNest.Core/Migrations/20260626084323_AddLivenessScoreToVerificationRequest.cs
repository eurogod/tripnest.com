using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripNest.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddLivenessScoreToVerificationRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "LivenessScore",
                table: "VerificationRequests",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LivenessScore",
                table: "VerificationRequests");
        }
    }
}
