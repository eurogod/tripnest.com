using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripNest.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddVerificationModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(2026, 6, 17, 20, 31, 41, 772, DateTimeKind.Utc).AddTicks(8146),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValue: new DateTime(2026, 6, 17, 20, 14, 39, 373, DateTimeKind.Utc).AddTicks(3357));

            migrationBuilder.CreateTable(
                name: "VerificationRequests",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    GhanaCardNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SelfiePhotoPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    NiaPhotoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FaceMatchScore = table.Column<double>(type: "double precision", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValue: new DateTime(2026, 6, 17, 20, 31, 41, 773, DateTimeKind.Utc).AddTicks(6158)),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificationRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VerificationRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VerificationRequests_GhanaCardNumber",
                table: "VerificationRequests",
                column: "GhanaCardNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VerificationRequests_Status",
                table: "VerificationRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationRequests_SubmittedAt",
                table: "VerificationRequests",
                column: "SubmittedAt");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationRequests_UserId",
                table: "VerificationRequests",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VerificationRequests");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(2026, 6, 17, 20, 14, 39, 373, DateTimeKind.Utc).AddTicks(3357),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValue: new DateTime(2026, 6, 17, 20, 31, 41, 772, DateTimeKind.Utc).AddTicks(8146));
        }
    }
}
