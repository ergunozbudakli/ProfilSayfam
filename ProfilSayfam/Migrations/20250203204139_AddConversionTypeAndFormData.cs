using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProfilSayfam.Migrations
{
    /// <inheritdoc />
    public partial class AddConversionTypeAndFormData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConversionType",
                table: "Conversions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FormData",
                table: "Conversions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConversionType",
                table: "Conversions");

            migrationBuilder.DropColumn(
                name: "FormData",
                table: "Conversions");
        }
    }
}
