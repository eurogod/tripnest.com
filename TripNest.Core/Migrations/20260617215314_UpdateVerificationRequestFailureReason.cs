using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripNest.Core.Migrations
{
    /// <inheritdoc />
    public partial class UpdateVerificationRequestFailureReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "SubmittedAt",
                table: "VerificationRequests",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(2026, 6, 17, 21, 53, 13, 260, DateTimeKind.Utc).AddTicks(4505),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValue: new DateTime(2026, 6, 17, 21, 6, 16, 331, DateTimeKind.Utc).AddTicks(1984));

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "VerificationRequests",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(2026, 6, 17, 21, 53, 13, 259, DateTimeKind.Utc).AddTicks(4175),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValue: new DateTime(2026, 6, 17, 21, 6, 16, 330, DateTimeKind.Utc).AddTicks(2182));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "VerificationRequests");

            migrationBuilder.AlterColumn<DateTime>(
                name: "SubmittedAt",
                table: "VerificationRequests",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(2026, 6, 17, 21, 6, 16, 331, DateTimeKind.Utc).AddTicks(1984),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValue: new DateTime(2026, 6, 17, 21, 53, 13, 260, DateTimeKind.Utc).AddTicks(4505));

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(2026, 6, 17, 21, 6, 16, 330, DateTimeKind.Utc).AddTicks(2182),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValue: new DateTime(2026, 6, 17, 21, 53, 13, 259, DateTimeKind.Utc).AddTicks(4175));
        }
    }
}
