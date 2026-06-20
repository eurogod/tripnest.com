using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripNest.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddModelGapItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CancellationPolicy",
                table: "Properties",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StayType",
                table: "Properties",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "Agreements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RentAmount",
                table: "Agreements",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "RentFrequency",
                table: "Agreements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "Agreements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PropertyCaretakerAssignments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    PropertyId = table.Column<string>(type: "text", nullable: false),
                    CaretakerId = table.Column<string>(type: "text", nullable: false),
                    AssignedByUserId = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyCaretakerAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyCaretakerAssignments_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PropertyCaretakerAssignments_Users_CaretakerId",
                        column: x => x.CaretakerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PropertyPhotos",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    PropertyId = table.Column<string>(type: "text", nullable: false),
                    PhotoPath = table.Column<string>(type: "text", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyPhotos_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyCaretakerAssignments_CaretakerId",
                table: "PropertyCaretakerAssignments",
                column: "CaretakerId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyCaretakerAssignments_PropertyId",
                table: "PropertyCaretakerAssignments",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyPhotos_PropertyId",
                table: "PropertyPhotos",
                column: "PropertyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PropertyCaretakerAssignments");

            migrationBuilder.DropTable(
                name: "PropertyPhotos");

            migrationBuilder.DropColumn(
                name: "CancellationPolicy",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "StayType",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "Agreements");

            migrationBuilder.DropColumn(
                name: "RentAmount",
                table: "Agreements");

            migrationBuilder.DropColumn(
                name: "RentFrequency",
                table: "Agreements");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Agreements");
        }
    }
}
