using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReservasSemestralesYAprobaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CorreosPendientes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Destinatario = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Asunto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CuerpoHtml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReferenciaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    Intentos = table.Column<int>(type: "int", nullable: false),
                    ProximoIntentoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EnviadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UltimoError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorreosPendientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FranjasNoOperativas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiaSemana = table.Column<int>(type: "int", nullable: true),
                    FechaEspecifica = table.Column<DateOnly>(type: "date", nullable: true),
                    HoraInicio = table.Column<TimeOnly>(type: "time", nullable: false),
                    HoraFin = table.Column<TimeOnly>(type: "time", nullable: false),
                    EsFeriado = table.Column<bool>(type: "bit", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranjasNoOperativas", x => x.Id);
                    table.CheckConstraint("CK_FranjasNoOperativas_Criterio", "([DiaSemana] IS NOT NULL AND [FechaEspecifica] IS NULL) OR ([DiaSemana] IS NULL AND [FechaEspecifica] IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "PeriodosAcademicos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FechaInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFin = table.Column<DateOnly>(type: "date", nullable: false),
                    EstadoActivo = table.Column<bool>(type: "bit", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodosAcademicos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReservasEspacio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EspacioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TitularId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Origen = table.Column<int>(type: "int", nullable: false),
                    OrigenId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Inicio = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Fin = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    CreadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservasEspacio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReservasEspacio_AspNetUsers",
                        column: x => x.TitularId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReservasEspacio_Espacios",
                        column: x => x.EspacioId,
                        principalTable: "Espacios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReservasRecurrentes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EspacioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocenteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoRecurrencia = table.Column<int>(type: "int", nullable: false),
                    DiaSemana = table.Column<int>(type: "int", nullable: true),
                    FechasFijas = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    HoraInicio = table.Column<TimeOnly>(type: "time", nullable: false),
                    HoraFin = table.Column<TimeOnly>(type: "time", nullable: false),
                    PeriodoInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodoFin = table.Column<DateOnly>(type: "date", nullable: false),
                    Materia = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservasRecurrentes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReservasRecurrentes_AspNetUsers",
                        column: x => x.DocenteId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReservasRecurrentes_Espacios",
                        column: x => x.EspacioId,
                        principalTable: "Espacios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SolicitudesPrestamo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SolicitanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EspacioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Inicio = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Fin = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Materia = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DocenteResponsable = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Justificacion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ArchivoRuta = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ArchivoNombreOriginal = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ArchivoTamanoBytes = table.Column<long>(type: "bigint", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    RadicadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevisadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevisorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MotivoRechazo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitudesPrestamo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolicitudesPrestamo_Espacios",
                        column: x => x.EspacioId,
                        principalTable: "Espacios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SolicitudesPrestamo_Revisor",
                        column: x => x.RevisorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SolicitudesPrestamo_Solicitante",
                        column: x => x.SolicitanteId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CorreosPendientes_Estado_ProximoIntento",
                table: "CorreosPendientes",
                columns: new[] { "Estado", "ProximoIntentoEn" });

            migrationBuilder.CreateIndex(
                name: "IX_CorreosPendientes_ReferenciaId",
                table: "CorreosPendientes",
                column: "ReferenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_FranjasNoOperativas_Vigencia",
                table: "FranjasNoOperativas",
                columns: new[] { "Activa", "DiaSemana", "FechaEspecifica" });

            migrationBuilder.CreateIndex(
                name: "IX_PeriodosAcademicos_Activo",
                table: "PeriodosAcademicos",
                column: "EstadoActivo",
                unique: true,
                filter: "[EstadoActivo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodosAcademicos_Rango",
                table: "PeriodosAcademicos",
                columns: new[] { "FechaInicio", "FechaFin" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservasEspacio_EspacioId_Inicio_Activa",
                table: "ReservasEspacio",
                columns: new[] { "EspacioId", "Inicio" },
                unique: true,
                filter: "[Estado] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ReservasEspacio_EspacioId_Ventana",
                table: "ReservasEspacio",
                columns: new[] { "EspacioId", "Inicio", "Fin" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservasEspacio_Origen",
                table: "ReservasEspacio",
                columns: new[] { "Origen", "OrigenId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservasEspacio_TitularId",
                table: "ReservasEspacio",
                column: "TitularId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservasRecurrentes_DocenteId_CreadaEn",
                table: "ReservasRecurrentes",
                columns: new[] { "DocenteId", "CreadaEn" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservasRecurrentes_EspacioId_Periodo",
                table: "ReservasRecurrentes",
                columns: new[] { "EspacioId", "PeriodoInicio" });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesPrestamo_EspacioId_Ventana",
                table: "SolicitudesPrestamo",
                columns: new[] { "EspacioId", "Inicio", "Fin" });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesPrestamo_Estado_RadicadaEn",
                table: "SolicitudesPrestamo",
                columns: new[] { "Estado", "RadicadaEn" });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesPrestamo_RevisorId",
                table: "SolicitudesPrestamo",
                column: "RevisorId");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesPrestamo_SolicitanteId_RadicadaEn",
                table: "SolicitudesPrestamo",
                columns: new[] { "SolicitanteId", "RadicadaEn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CorreosPendientes");

            migrationBuilder.DropTable(
                name: "FranjasNoOperativas");

            migrationBuilder.DropTable(
                name: "PeriodosAcademicos");

            migrationBuilder.DropTable(
                name: "ReservasEspacio");

            migrationBuilder.DropTable(
                name: "ReservasRecurrentes");

            migrationBuilder.DropTable(
                name: "SolicitudesPrestamo");
        }
    }
}
