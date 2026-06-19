using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripNest.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddConcurrencyAndIntegrityConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use the DB clock for the default instead of a constant baked at model-build time.
            migrationBuilder.AlterColumn<DateTime>(
                name: "SubmittedAt",
                table: "VerificationRequests",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValue: new DateTime(2026, 6, 18, 15, 39, 39, 627, DateTimeKind.Utc).AddTicks(2990));

            // NOTE: the Booking/Escrow "Version" concurrency tokens map to Postgres' built-in
            // `xmin` system column, which already exists on every table — so there is no
            // AddColumn here (creating a column named xmin would be rejected by Postgres).

            // Authoritative guard against double-booking: no two CONFIRMED bookings for the same
            // property may have overlapping date ranges. This closes the TOCTOU race that the
            // application-level overlap check cannot.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");
            migrationBuilder.Sql(@"
                ALTER TABLE ""Bookings""
                ADD CONSTRAINT ""no_overlapping_confirmed_bookings""
                EXCLUDE USING gist (
                    ""PropertyId"" WITH =,
                    tstzrange(""CheckInDate"", ""CheckOutDate"") WITH &&
                ) WHERE (""Status"" = 1);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Bookings"" DROP CONSTRAINT IF EXISTS ""no_overlapping_confirmed_bookings"";");

            migrationBuilder.AlterColumn<DateTime>(
                name: "SubmittedAt",
                table: "VerificationRequests",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(2026, 6, 18, 15, 39, 39, 627, DateTimeKind.Utc).AddTicks(2990),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");
        }
    }
}
