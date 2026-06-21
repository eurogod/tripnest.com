using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripNest.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailOtpTrustedContactRemoveWhatsApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SentViaWhatsApp",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "WhatsAppEnabled",
                table: "CommunicationPreferences");

            migrationBuilder.AddColumn<int>(
                name: "EmailOtpAttempts",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailOtpExpiry",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailOtpHash",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmailVerified",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TrustedContactEmail",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrustedContactName",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrustedContactPhone",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "SafetyCheckIns",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LocationShared",
                table: "SafetyCheckIns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "SafetyCheckIns",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailOtpAttempts",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmailOtpExpiry",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmailOtpHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmailVerified",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TrustedContactEmail",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TrustedContactName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TrustedContactPhone",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "SafetyCheckIns");

            migrationBuilder.DropColumn(
                name: "LocationShared",
                table: "SafetyCheckIns");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "SafetyCheckIns");

            migrationBuilder.AddColumn<bool>(
                name: "SentViaWhatsApp",
                table: "Notifications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "WhatsAppEnabled",
                table: "CommunicationPreferences",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
