using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripNest.Core.Migrations
{
    /// <inheritdoc />
    public partial class ProfileSignaturesAndAgreementIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SignatureImagePath",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SignatureUpdatedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LandlordSignatureImagePath",
                table: "Agreements",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantSignatureImagePath",
                table: "Agreements",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermsHash",
                table: "Agreements",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SignatureImagePath",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SignatureUpdatedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LandlordSignatureImagePath",
                table: "Agreements");

            migrationBuilder.DropColumn(
                name: "TenantSignatureImagePath",
                table: "Agreements");

            migrationBuilder.DropColumn(
                name: "TermsHash",
                table: "Agreements");
        }
    }
}
