using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Reservas.Dtos;
using Backend.Domain.Exceptions;
using Backend.Domain.Services;
using Backend.Domain.ValueObjects;

namespace Backend.Application.Modules.Reservas;

/// <summary>
/// Consulta la disponibilidad de un puesto para una fecha y jornada, sin exponer la agenda
/// de otros usuarios: solo devuelve franjas con su motivo de bloqueo.
/// </summary>
public sealed class ConsultarDisponibilidadHandler
{
    private readonly IConsultaReservas _consulta;
    private readonly OpcionesReservas _opciones;
    private readonly IRelojSistema _reloj;

    /// <summary>Inicializa el caso de uso con sus puertos y la política institucional.</summary>
    public ConsultarDisponibilidadHandler(
        IConsultaReservas consulta,
        OpcionesReservas opciones,
        IRelojSistema reloj)
    {
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
        _opciones = opciones ?? throw new ArgumentNullException(nameof(opciones));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
    }

    /// <summary>Devuelve las franjas del puesto con su disponibilidad calculada.</summary>
    public async Task<Resultado<DisponibilidadPuesto>> HandleAsync(
        Guid puestoId,
        FiltroDisponibilidad filtro,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var puesto = await _consulta.ObtenerPuestoAsync(puestoId, cancellationToken);

        if (puesto is null)
        {
            return Resultado<DisponibilidadPuesto>.Fallo(TipoFallo.NoEncontrado, MensajesReserva.PuestoNoEncontrado);
        }

        if (!puesto.AdmitePrestamoDirecto)
        {
            return Resultado<DisponibilidadPuesto>.Fallo(TipoFallo.Conflicto, MensajesReserva.PuestoNoReservable);
        }

        var zona = _opciones.ObtenerZonaHoraria();
        var politica = _opciones.ObtenerPolitica();
        var ahora = _reloj.Ahora;
        var hoyLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(ahora, zona).DateTime);

        if (filtro.Fecha < hoyLocal || filtro.Fecha > hoyLocal.AddDays(_opciones.AntelacionMaximaDias))
        {
            return Resultado<DisponibilidadPuesto>.Fallo(
                TipoFallo.Validacion,
                MensajesReserva.FechaFueraDeHorizonte,
                MapeoReservas.Errores("fecha", MensajesReserva.FechaFueraDeHorizonte));
        }

        var apertura = MotorDisponibilidad.InicioEnZona(filtro.Fecha, politica.HoraApertura, zona);
        var cierre = MotorDisponibilidad.InicioEnZona(filtro.Fecha, politica.HoraCierre, zona);

        var ocupadas = (await _consulta.ObtenerOcupadasAsync(puestoId, apertura, cierre, cancellationToken))
            .Select(intervalo => FranjaHoraria.Desde(intervalo.Inicio, intervalo.Fin))
            .ToArray();

        var franjas = MotorDisponibilidad
            .GenerarFranjas(
                filtro.Fecha,
                politica,
                zona,
                ocupadas,
                ahora,
                filtro.DuracionMinutos,
                filtro.Desde,
                filtro.Hasta)
            .Select(candidata => new FranjaDisponible(
                candidata.Franja.Inicio,
                candidata.Franja.Fin,
                candidata.Disponible,
                NombreMotivo(candidata.Motivo)))
            .ToArray();

        return Resultado<DisponibilidadPuesto>.Exito(new DisponibilidadPuesto(
            puesto.PuestoId,
            puesto.Identificador,
            puesto.EspacioId,
            puesto.EspacioNombre,
            _opciones.ZonaHoraria,
            franjas));
    }

    private static string? NombreMotivo(MotivoNoDisponible? motivo) => motivo switch
    {
        MotivoNoDisponible.Ocupada => nameof(MotivoNoDisponible.Ocupada),
        MotivoNoDisponible.FueraDeAntelacion => nameof(MotivoNoDisponible.FueraDeAntelacion),
        MotivoNoDisponible.FueraDeHorizonte => nameof(MotivoNoDisponible.FueraDeHorizonte),
        _ => null
    };
}
