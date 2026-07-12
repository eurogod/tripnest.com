using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripNest.Core.Migrations
{
    /// <inheritdoc />
    public partial class DynamicPricingClaimsUrgentSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FirstRespondedAt",
                table: "SupportTicket",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsUrgent",
                table: "SupportTicket",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DynamicPricingEnabled",
                table: "PricingSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxNightlyRate",
                table: "PricingSettings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MinNightlyRate",
                table: "PricingSettings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DamageClaimId",
                table: "Payouts",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DamageClaims",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    BookingId = table.Column<string>(type: "text", nullable: false),
                    LandlordId = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ApprovedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PhotoPaths = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TenantResponse = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DamageClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DamageClaims_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DemandEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpliftPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemandEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_DamageClaimId",
                table: "Payouts",
                column: "DamageClaimId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DamageClaims_BookingId",
                table: "DamageClaims",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DamageClaims_LandlordId",
                table: "DamageClaims",
                column: "LandlordId");

            migrationBuilder.CreateIndex(
                name: "IX_DamageClaims_Status",
                table: "DamageClaims",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_Payouts_DamageClaims_DamageClaimId",
                table: "Payouts",
                column: "DamageClaimId",
                principalTable: "DamageClaims",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payouts_DamageClaims_DamageClaimId",
                table: "Payouts");

            migrationBuilder.DropTable(
                name: "DamageClaims");

            migrationBuilder.DropTable(
                name: "DemandEvents");

            migrationBuilder.DropIndex(
                name: "IX_Payouts_DamageClaimId",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "FirstRespondedAt",
                table: "SupportTicket");

            migrationBuilder.DropColumn(
                name: "IsUrgent",
                table: "SupportTicket");

            migrationBuilder.DropColumn(
                name: "DynamicPricingEnabled",
                table: "PricingSettings");

            migrationBuilder.DropColumn(
                name: "MaxNightlyRate",
                table: "PricingSettings");

            migrationBuilder.DropColumn(
                name: "MinNightlyRate",
                table: "PricingSettings");

            migrationBuilder.DropColumn(
                name: "DamageClaimId",
                table: "Payouts");
        }
    }
}
