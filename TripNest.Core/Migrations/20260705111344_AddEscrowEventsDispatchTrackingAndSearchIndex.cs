using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripNest.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddEscrowEventsDispatchTrackingAndSearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PendingEmailDispatch",
                table: "Notifications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PendingSmsDispatch",
                table: "Notifications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "EscrowEvent",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    EscrowId = table.Column<string>(type: "text", nullable: false),
                    BookingId = table.Column<string>(type: "text", nullable: false),
                    FromStatus = table.Column<int>(type: "integer", nullable: false),
                    ToStatus = table.Column<int>(type: "integer", nullable: false),
                    Actor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscrowEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EscrowEvent_Escrows_EscrowId",
                        column: x => x.EscrowId,
                        principalTable: "Escrows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EscrowEvent_BookingId",
                table: "EscrowEvent",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_EscrowEvent_EscrowId",
                table: "EscrowEvent",
                column: "EscrowId");

            // Trigram index so the property search's case-insensitive substring match
            // (lower("Location") LIKE '%…%') is served by an index instead of a sequential scan.
            // Follows the pattern of the btree_gist exclusion-constraint migration: extension
            // creation is idempotent, and the expression matches EF's ToLower().Contains translation.
            migrationBuilder.Sql("""CREATE EXTENSION IF NOT EXISTS pg_trgm;""");
            migrationBuilder.Sql(
                """CREATE INDEX "IX_Properties_Location_trgm" ON "Properties" USING gin (lower("Location") gin_trgm_ops);""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Properties_Location_trgm";""");

            migrationBuilder.DropTable(
                name: "EscrowEvent");

            migrationBuilder.DropColumn(
                name: "PendingEmailDispatch",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "PendingSmsDispatch",
                table: "Notifications");
        }
    }
}
