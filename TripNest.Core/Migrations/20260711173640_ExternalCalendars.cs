using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripNest.Core.Migrations
{
    /// <inheritdoc />
    public partial class ExternalCalendars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalCalendarId",
                table: "PropertyBlockedDates",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExternalCalendars",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    PropertyId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FeedUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSyncError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalCalendars", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalCalendars_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyBlockedDates_ExternalCalendarId",
                table: "PropertyBlockedDates",
                column: "ExternalCalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalCalendars_PropertyId",
                table: "ExternalCalendars",
                column: "PropertyId");

            migrationBuilder.AddForeignKey(
                name: "FK_PropertyBlockedDates_ExternalCalendars_ExternalCalendarId",
                table: "PropertyBlockedDates",
                column: "ExternalCalendarId",
                principalTable: "ExternalCalendars",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PropertyBlockedDates_ExternalCalendars_ExternalCalendarId",
                table: "PropertyBlockedDates");

            migrationBuilder.DropTable(
                name: "ExternalCalendars");

            migrationBuilder.DropIndex(
                name: "IX_PropertyBlockedDates_ExternalCalendarId",
                table: "PropertyBlockedDates");

            migrationBuilder.DropColumn(
                name: "ExternalCalendarId",
                table: "PropertyBlockedDates");
        }
    }
}
