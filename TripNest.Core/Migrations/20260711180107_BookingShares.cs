using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripNest.Core.Migrations
{
    /// <inheritdoc />
    public partial class BookingShares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookingShares",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    BookingId = table.Column<string>(type: "text", nullable: false),
                    ParticipantUserId = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PaymentReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingShares_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookingShares_Users_ParticipantUserId",
                        column: x => x.ParticipantUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingShares_BookingId",
                table: "BookingShares",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingShares_BookingId_ParticipantUserId",
                table: "BookingShares",
                columns: new[] { "BookingId", "ParticipantUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingShares_ParticipantUserId",
                table: "BookingShares",
                column: "ParticipantUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingShares");
        }
    }
}
