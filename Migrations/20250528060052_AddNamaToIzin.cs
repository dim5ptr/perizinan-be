using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PresensiQRBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddNamaToIzin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Nama",
                table: "Izins",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Nama",
                table: "Izins");
        }
    }
}
