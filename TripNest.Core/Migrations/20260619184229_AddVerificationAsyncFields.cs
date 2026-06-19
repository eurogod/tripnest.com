using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripNest.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddVerificationAsyncFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ClaimedDateOfBirth",
                table: "VerificationRequests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClaimedFirstName",
                table: "VerificationRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClaimedLastName",
                table: "VerificationRequests",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClaimedDateOfBirth",
                table: "VerificationRequests");

            migrationBuilder.DropColumn(
                name: "ClaimedFirstName",
                table: "VerificationRequests");

            migrationBuilder.DropColumn(
                name: "ClaimedLastName",
                table: "VerificationRequests");
        }
    }
}
