using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripNest.Core.Migrations
{
    /// <inheritdoc />
    public partial class RentInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "EscrowId",
                table: "Payouts",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "RentInvoiceId",
                table: "Payouts",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RentInvoices",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    BookingId = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    LandlordId = table.Column<string>(type: "text", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PaymentReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RentInvoices_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_RentInvoiceId",
                table: "Payouts",
                column: "RentInvoiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RentInvoices_BookingId",
                table: "RentInvoices",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_RentInvoices_BookingId_PeriodStart",
                table: "RentInvoices",
                columns: new[] { "BookingId", "PeriodStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RentInvoices_Status_DueDate",
                table: "RentInvoices",
                columns: new[] { "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RentInvoices_TenantId",
                table: "RentInvoices",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payouts_RentInvoices_RentInvoiceId",
                table: "Payouts",
                column: "RentInvoiceId",
                principalTable: "RentInvoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payouts_RentInvoices_RentInvoiceId",
                table: "Payouts");

            migrationBuilder.DropTable(
                name: "RentInvoices");

            migrationBuilder.DropIndex(
                name: "IX_Payouts_RentInvoiceId",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "RentInvoiceId",
                table: "Payouts");

            migrationBuilder.AlterColumn<string>(
                name: "EscrowId",
                table: "Payouts",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
