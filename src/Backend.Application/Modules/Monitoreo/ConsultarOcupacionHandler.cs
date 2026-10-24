using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Monitoreo.Dtos;
using Backend.Domain.Enums;

namespace Backend.Application.Modules.Monitoreo;

/// <summary>
/// Agrega la foto instantánea de ocupación por área: puestos en uso dentro de su franja,
/// pendientes de check-in dentro de la ventana de tolerancia, sesiones que ya pasaron su fin sin
/// salida y puestos libres. Una sesión olvidada se informa, pero no infla la ocupación.
/// </summary>
public sealed class ConsultarOcupacionHandler
{
    private readonly IConsultaOcupacionActual _consulta;
    private readonly OpcionesReservas _opciones;
    private readonly IRelojSistema _reloj;

    /// <summary>Inicializa el caso de uso con la consulta, la configuración y el reloj del servidor.</summary>
    public ConsultarOcupacionHandler(
        IConsultaOcupacionActual consulta,
        OpcionesReservas opciones,
        IRelojSistema reloj)
    {
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
        _opciones = opciones ?? throw new ArgumentNullException(nameof(opciones));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
    }

    /// <summary>Devuelve la ocupación del campus en el instante consultado.</summary>
    public async Task<Resultado<OcupacionActual>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var ahora = _reloj.Ahora;
        var tolerancia = _opciones.ObtenerVentanaTolerancia();

        var instantanea = await _consulta.ObtenerAsync(ahora, tolerancia, cancellationToken);

        var puestosPorArea = instantanea.Puestos
            .GroupBy(puesto => puesto.EspacioId)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.ToArray());

        var reservasPorArea = instantanea.Reservas
            .GroupBy(reserva => reserva.EspacioId)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.ToArray());

        var identificadores = instantanea.Puestos
            .ToDictionary(puesto => puesto.PuestoId, puesto => puesto.Identificador);

        var areas = instantanea.Areas
            .OrderBy(area => area.Nombre, StringComparer.Ordinal)
            .Select(area => Agregar(
                area,
                puestosPorArea.GetValueOrDefault(area.EspacioId) ?? [],
                reservasPorArea.GetValueOrDefault(area.EspacioId) ?? [],
                identificadores,
                ahora,
                tolerancia))
            .ToArray();

        var totales = new TotalesOcupacion(
            areas.Sum(area => area.Aforo),
            areas.Sum(area => area.PuestosActivos),
            areas.Sum(area => area.EnUso),
            areas.Sum(area => area.PendientesCheckIn),
            areas.Sum(area => area.SesionesSinCerrar),
            areas.Sum(area => area.Libres),
            Porcentaje(areas.Sum(area => area.EnUso), areas.Sum(area => area.PuestosActivos)));

        return Resultado<OcupacionActual>.Exito(new OcupacionActual(
            ahora,
            (int)tolerancia.TotalMinutes,
            totales,
            areas));
    }

    private static AreaOcupacion Agregar(
        AreaOcupable area,
        IReadOnlyList<PuestoOcupable> puestos,
        IReadOnlyList<ReservaOcupacionActual> reservas,
        IReadOnlyDictionary<Guid, string> identificadores,
        DateTimeOffset ahora,
        TimeSpan tolerancia)
    {
        var enCurso = reservas
            .Where(reserva => EnUsoAhora(reserva, ahora))
            .OrderBy(reserva => reserva.Fin)
            .ThenBy(reserva => reserva.ReservaId)
            .ToArray();

        var pendientes = reservas
            .Where(reserva => PendienteDeCheckIn(reserva, ahora, tolerancia))
            .ToArray();

        var sinCerrar = reservas
            .Where(reserva => reserva.Estado == EstadoReserva.EnUso && reserva.Fin <= ahora)
            .ToArray();

        var enUso = enCurso.Length;
        var puestosActivos = puestos.Count;
        var libres = Math.Max(0, puestosActivos - enUso - pendientes.Length);

        var proximaLiberacion = enCurso
            .Select(reserva => (DateTimeOffset?)reserva.Fin)
            .Concat(pendientes.Select(reserva => (DateTimeOffset?)(reserva.Inicio + tolerancia)))
            .DefaultIfEmpty(null)
            .Min();

        return new AreaOcupacion(
            area.EspacioId,
            area.Nombre,
            area.Aforo,
            puestosActivos,
            enUso,
            pendientes.Length,
            sinCerrar.Length,
            libres,
            Porcentaje(enUso, puestosActivos),
            proximaLiberacion,
            enCurso
                .Select(reserva => new SesionEnCurso(
                    reserva.ReservaId,
                    identificadores.GetValueOrDefault(reserva.PuestoId, string.Empty),
                    reserva.Titular,
                    reserva.Inicio,
                    reserva.Fin,
                    reserva.FechaCheckIn))
                .ToArray());
    }

    private static bool EnUsoAhora(ReservaOcupacionActual reserva, DateTimeOffset ahora) =>
        reserva.Estado == EstadoReserva.EnUso
        && reserva.Inicio <= ahora
        && reserva.Fin > ahora;

    private static bool PendienteDeCheckIn(
        ReservaOcupacionActual reserva,
        DateTimeOffset ahora,
        TimeSpan tolerancia) =>
        reserva.Estado == EstadoReserva.Confirmada
        && ahora >= reserva.Inicio - tolerancia
        && ahora <= reserva.Inicio + tolerancia;

    private static decimal Porcentaje(int enUso, int puestosActivos) =>
        puestosActivos == 0
            ? 0m
            : Math.Round(enUso * 100m / puestosActivos, 2, MidpointRounding.AwayFromZero);
}
