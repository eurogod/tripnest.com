using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripNest.Core.Migrations
{
    /// <inheritdoc />
    public partial class StudentVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PendingStudentEmail",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentEmail",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StudentOtpAttempts",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "StudentOtpExpiry",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentOtpHash",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StudentVerifiedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PendingStudentEmail",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "StudentEmail",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "StudentOtpAttempts",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "StudentOtpExpiry",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "StudentOtpHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "StudentVerifiedAt",
                table: "Users");
        }
    }
}
