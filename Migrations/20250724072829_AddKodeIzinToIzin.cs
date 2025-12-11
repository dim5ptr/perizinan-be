using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PresensiQRBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddKodeIzinToIzin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KodeIzin",
                table: "Izins",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Izins_UserId",
                table: "Izins",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Izins_Users_UserId",
                table: "Izins",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Izins_Users_UserId",
                table: "Izins");

            migrationBuilder.DropIndex(
                name: "IX_Izins_UserId",
                table: "Izins");

            migrationBuilder.DropColumn(
                name: "KodeIzin",
                table: "Izins");
        }
    }
}
