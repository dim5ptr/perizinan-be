using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PresensiQRBackend.Migrations
{
    /// <inheritdoc />
    public partial class FixJenisIzinType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "CheckInTime",
                table: "Presensis",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Presensis",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JenisIzin",
                table: "Izins",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Presensis");

            migrationBuilder.DropColumn(
                name: "JenisIzin",
                table: "Izins");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CheckInTime",
                table: "Presensis",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }
    }
}
