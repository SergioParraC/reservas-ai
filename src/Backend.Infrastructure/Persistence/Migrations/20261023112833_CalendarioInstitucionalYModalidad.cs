using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CalendarioInstitucionalYModalidad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TipoElementoPrestamo",
                table: "Espacios",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "TipoModalidadPrestamo",
                table: "Espacios",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // Relleno heurístico de las áreas existentes: la categoría funcional y la presencia
            // de puestos deciden la modalidad y el elemento iniciales sin tocar el catálogo.
            migrationBuilder.Sql("""
                UPDATE [Espacios]
                SET [TipoModalidadPrestamo] = CASE
                        WHEN [Categoria] = 4 THEN 2
                        WHEN [Categoria] = 6 THEN 3
                        ELSE 1
                    END,
                    [TipoElementoPrestamo] = CASE
                        WHEN [Categoria] = 4 THEN 1
                        WHEN [Categoria] = 3 THEN 4
                        WHEN [Categoria] = 2 THEN 2
                        ELSE 3
                    END;

                UPDATE [Espacios]
                SET [TipoModalidadPrestamo] = 2,
                    [TipoElementoPrestamo] = 1
                WHERE [TipoModalidadPrestamo] = 1
                  AND EXISTS (
                      SELECT 1
                      FROM [Puestos] AS [puesto]
                      WHERE [puesto].[EspacioId] = [Espacios].[Id]
                        AND [puesto].[Estado] = 1);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TipoElementoPrestamo",
                table: "Espacios");

            migrationBuilder.DropColumn(
                name: "TipoModalidadPrestamo",
                table: "Espacios");
        }
    }
}
