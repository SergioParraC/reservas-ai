using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AreasYPuestos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Espacios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Ubicacion = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    Aforo = table.Column<int>(type: "int", nullable: false),
                    TipoAprobacion = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    CodigoQR = table.Column<string>(type: "nvarchar(29)", maxLength: 29, nullable: false),
                    FotografiaRuta = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Espacios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Puestos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EspacioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Identificador = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DescripcionEquipo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CodigoQR = table.Column<string>(type: "nvarchar(29)", maxLength: 29, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Puestos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Puestos_Espacios_EspacioId",
                        column: x => x.EspacioId,
                        principalTable: "Espacios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Espacios_CodigoQR",
                table: "Espacios",
                column: "CodigoQR",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Espacios_Estado_Categoria_Aforo",
                table: "Espacios",
                columns: new[] { "Estado", "Categoria", "Aforo" });

            migrationBuilder.CreateIndex(
                name: "IX_Espacios_Nombre_Ubicacion",
                table: "Espacios",
                columns: new[] { "Nombre", "Ubicacion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Puestos_CodigoQR",
                table: "Puestos",
                column: "CodigoQR",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Puestos_EspacioId_Identificador",
                table: "Puestos",
                columns: new[] { "EspacioId", "Identificador" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Puestos");

            migrationBuilder.DropTable(
                name: "Espacios");
        }
    }
}
