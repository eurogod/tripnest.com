using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripNest.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddFinalModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "SubmittedAt",
                table: "VerificationRequests",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(2026, 6, 17, 21, 6, 16, 331, DateTimeKind.Utc).AddTicks(1984),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValue: new DateTime(2026, 6, 17, 20, 39, 55, 295, DateTimeKind.Utc).AddTicks(1498));

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(2026, 6, 17, 21, 6, 16, 330, DateTimeKind.Utc).AddTicks(2182),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValue: new DateTime(2026, 6, 17, 20, 39, 55, 294, DateTimeKind.Utc).AddTicks(7224));

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OldValue = table.Column<string>(type: "text", nullable: true),
                    NewValue = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IpAddress = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SafetyCheckIns",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    BookingId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    EmergencyContactPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CheckedInAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AlertSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyCheckIns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SafetyCheckIns_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StayFeedbacks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    BookingId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    PropertyId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    LandlordId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    AccuracyRating = table.Column<int>(type: "integer", nullable: false),
                    CleanlinessRating = table.Column<int>(type: "integer", nullable: false),
                    SafetyRating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StayFeedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StayFeedbacks_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StayFeedbacks_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StayFeedbacks_Users_LandlordId",
                        column: x => x.LandlordId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StayFeedbacks_Users_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrustScoreSnapshots",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    SubjectType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SubjectId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    VerificationComponent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    HistoryComponent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    FeedbackComponent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    FinalScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Trend = table.Column<int>(type: "integer", nullable: false),
                    SnapshotDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrustScoreSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyCheckIns_BookingId",
                table: "SafetyCheckIns",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StayFeedbacks_BookingId",
                table: "StayFeedbacks",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StayFeedbacks_LandlordId",
                table: "StayFeedbacks",
                column: "LandlordId");

            migrationBuilder.CreateIndex(
                name: "IX_StayFeedbacks_PropertyId",
                table: "StayFeedbacks",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_StayFeedbacks_TenantId",
                table: "StayFeedbacks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TrustScoreSnapshots_SubjectType_SubjectId_SnapshotDate",
                table: "TrustScoreSnapshots",
                columns: new[] { "SubjectType", "SubjectId", "SnapshotDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "SafetyCheckIns");

            migrationBuilder.DropTable(
                name: "StayFeedbacks");

            migrationBuilder.DropTable(
                name: "TrustScoreSnapshots");

            migrationBuilder.AlterColumn<DateTime>(
                name: "SubmittedAt",
                table: "VerificationRequests",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(2026, 6, 17, 20, 39, 55, 295, DateTimeKind.Utc).AddTicks(1498),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValue: new DateTime(2026, 6, 17, 21, 6, 16, 331, DateTimeKind.Utc).AddTicks(1984));

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(2026, 6, 17, 20, 39, 55, 294, DateTimeKind.Utc).AddTicks(7224),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValue: new DateTime(2026, 6, 17, 21, 6, 16, 330, DateTimeKind.Utc).AddTicks(2182));
        }
    }
}
