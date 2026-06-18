using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripNest.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddWalkthroughApprovalGate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "VideoUrl",
                table: "Walkthroughs",
                newName: "VideoPath");

            migrationBuilder.AlterColumn<DateTime>(
                name: "SubmittedAt",
                table: "VerificationRequests",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(2026, 6, 18, 15, 17, 14, 137, DateTimeKind.Utc).AddTicks(8897),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValue: new DateTime(2026, 6, 18, 14, 46, 42, 118, DateTimeKind.Utc).AddTicks(9158));

            migrationBuilder.AddColumn<string>(
                name: "WalkthroughRejectionReason",
                table: "Properties",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WalkthroughReviewedAt",
                table: "Properties",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WalkthroughReviewedById",
                table: "Properties",
                type: "character varying(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WalkthroughStatus",
                table: "Properties",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "WalkthroughVideoPath",
                table: "Properties",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Properties_WalkthroughReviewedById",
                table: "Properties",
                column: "WalkthroughReviewedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Properties_Users_WalkthroughReviewedById",
                table: "Properties",
                column: "WalkthroughReviewedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Properties_Users_WalkthroughReviewedById",
                table: "Properties");

            migrationBuilder.DropIndex(
                name: "IX_Properties_WalkthroughReviewedById",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "WalkthroughRejectionReason",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "WalkthroughReviewedAt",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "WalkthroughReviewedById",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "WalkthroughStatus",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "WalkthroughVideoPath",
                table: "Properties");

            migrationBuilder.RenameColumn(
                name: "VideoPath",
                table: "Walkthroughs",
                newName: "VideoUrl");

            migrationBuilder.AlterColumn<DateTime>(
                name: "SubmittedAt",
                table: "VerificationRequests",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(2026, 6, 18, 14, 46, 42, 118, DateTimeKind.Utc).AddTicks(9158),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValue: new DateTime(2026, 6, 18, 15, 17, 14, 137, DateTimeKind.Utc).AddTicks(8897));
        }
    }
}
