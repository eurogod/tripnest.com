using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripNest.Core.Migrations
{
    /// <inheritdoc />
    public partial class AgentCaretakerMarketplaceUpgrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Caretakers_Properties_PropertyId",
                table: "Caretakers");

            migrationBuilder.DropForeignKey(
                name: "FK_PropertyCaretakerAssignments_Users_CaretakerId",
                table: "PropertyCaretakerAssignments");

            migrationBuilder.AddColumn<int>(
                name: "Rating",
                table: "ViewingRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewComment",
                table: "ViewingRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndedAt",
                table: "PropertyCaretakerAssignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PropertyId",
                table: "Caretakers",
                type: "character varying(36)",
                maxLength: 36,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(36)",
                oldMaxLength: 36);

            migrationBuilder.AddColumn<string>(
                name: "Bio",
                table: "Caretakers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServiceArea",
                table: "Caretakers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServiceArea",
                table: "Agents",
                type: "text",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Caretakers_Properties_PropertyId",
                table: "Caretakers",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // The old FK pointed at Users while the app stored Caretaker entity ids, so any rows
            // written while the constraint was unenforced (or via seed data) may not resolve to a
            // caretaker. Purge them before retargeting the FK at Caretakers.
            migrationBuilder.Sql(
                """DELETE FROM "PropertyCaretakerAssignments" WHERE "CaretakerId" NOT IN (SELECT "Id" FROM "Caretakers");""");

            migrationBuilder.AddForeignKey(
                name: "FK_PropertyCaretakerAssignments_Caretakers_CaretakerId",
                table: "PropertyCaretakerAssignments",
                column: "CaretakerId",
                principalTable: "Caretakers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Caretakers_Properties_PropertyId",
                table: "Caretakers");

            migrationBuilder.DropForeignKey(
                name: "FK_PropertyCaretakerAssignments_Caretakers_CaretakerId",
                table: "PropertyCaretakerAssignments");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "ViewingRequests");

            migrationBuilder.DropColumn(
                name: "ReviewComment",
                table: "ViewingRequests");

            migrationBuilder.DropColumn(
                name: "EndedAt",
                table: "PropertyCaretakerAssignments");

            migrationBuilder.DropColumn(
                name: "Bio",
                table: "Caretakers");

            migrationBuilder.DropColumn(
                name: "ServiceArea",
                table: "Caretakers");

            migrationBuilder.DropColumn(
                name: "ServiceArea",
                table: "Agents");

            migrationBuilder.AlterColumn<string>(
                name: "PropertyId",
                table: "Caretakers",
                type: "character varying(36)",
                maxLength: 36,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(36)",
                oldMaxLength: 36,
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Caretakers_Properties_PropertyId",
                table: "Caretakers",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PropertyCaretakerAssignments_Users_CaretakerId",
                table: "PropertyCaretakerAssignments",
                column: "CaretakerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
