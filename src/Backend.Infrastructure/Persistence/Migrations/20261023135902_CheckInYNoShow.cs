using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CheckInYNoShow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reservas_PuestoId_Inicio_Activa",
                table: "Reservas");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FechaCheckIn",
                table: "Reservas",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FechaCheckOut",
                table: "Reservas",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FinPlanificado",
                table: "Reservas",
                type: "datetimeoffset",
                nullable: true);

            // Migración aditiva sobre datos existentes: el fin pactado de toda fila previa era su
            // propio Fin, que hasta esta etapa nunca se acortaba.
            migrationBuilder.Sql("UPDATE [Reservas] SET [FinPlanificado] = [Fin] WHERE [FinPlanificado] IS NULL;");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "FinPlanificado",
                table: "Reservas",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_Estado_Inicio",
                table: "Reservas",
                columns: new[] { "Estado", "Inicio" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_PuestoId_Inicio_Activa",
                table: "Reservas",
                columns: new[] { "PuestoId", "Inicio" },
                unique: true,
                filter: "[Estado] IN (1, 4)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reservas_Estado_Inicio",
                table: "Reservas");

            migrationBuilder.DropIndex(
                name: "IX_Reservas_PuestoId_Inicio_Activa",
                table: "Reservas");

            migrationBuilder.DropColumn(
                name: "FechaCheckIn",
                table: "Reservas");

            migrationBuilder.DropColumn(
                name: "FechaCheckOut",
                table: "Reservas");

            migrationBuilder.DropColumn(
                name: "FinPlanificado",
                table: "Reservas");

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_PuestoId_Inicio_Activa",
                table: "Reservas",
                columns: new[] { "PuestoId", "Inicio" },
                unique: true,
                filter: "[Estado] = 1");
        }
    }
}
