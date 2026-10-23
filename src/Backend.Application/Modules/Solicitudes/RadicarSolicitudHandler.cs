using System.Globalization;
using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Areas.Dtos;
using Backend.Application.Modules.Solicitudes.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Domain.Exceptions;
using Backend.Domain.ValueObjects;

namespace Backend.Application.Modules.Solicitudes;

/// <summary>
/// Radicación de una solicitud con su pre-informe PDF. El calendario y la modalidad del área se
/// validan antes de escribir el archivo; la base de datos guarda solo los metadatos.
/// </summary>
public sealed class RadicarSolicitudHandler
{
    private readonly IConsultaEspacios _espacios;
    private readonly IConsultaCalendario _calendario;
    private readonly IConsultaSolicitudes _consulta;
    private readonly ISolicitudesRepository _repositorio;
    private readonly IAlmacenDocumentos _documentos;
    private readonly OpcionesSolicitudes _solicitudes;
    private readonly OpcionesReservas _reservas;
    private readonly IRelojSistema _reloj;
    private readonly IUsuarioActual _usuario;

    /// <summary>Inicializa el caso de uso con sus puertos, la política y la identidad actual.</summary>
    public RadicarSolicitudHandler(
        IConsultaEspacios espacios,
        IConsultaCalendario calendario,
        IConsultaSolicitudes consulta,
        ISolicitudesRepository repositorio,
        IAlmacenDocumentos documentos,
        OpcionesSolicitudes solicitudes,
        OpcionesReservas reservas,
        IRelojSistema reloj,
        IUsuarioActual usuario)
    {
        _espacios = espacios ?? throw new ArgumentNullException(nameof(espacios));
        _calendario = calendario ?? throw new ArgumentNullException(nameof(calendario));
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _documentos = documentos ?? throw new ArgumentNullException(nameof(documentos));
        _solicitudes = solicitudes ?? throw new ArgumentNullException(nameof(solicitudes));
        _reservas = reservas ?? throw new ArgumentNullException(nameof(reservas));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _usuario = usuario ?? throw new ArgumentNullException(nameof(usuario));
    }

    /// <summary>Registra la solicitud en estado <c>Radicado</c> y almacena el pre-informe.</summary>
    public async Task<Resultado<SolicitudRadicada>> HandleAsync(
        ComandoRadicarSolicitud comando,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        if (_usuario.UsuarioId is not { } solicitante)
        {
            return Resultado<SolicitudRadicada>.Fallo(TipoFallo.NoAutorizado, MensajesReserva.SinSesion);
        }

        if (ValidacionesSolicitud.ValidarArchivo(comando, _solicitudes) is { } errorArchivo)
        {
            return Resultado<SolicitudRadicada>.Fallo(
                errorArchivo.Tipo,
                errorArchivo.Mensaje,
                MapeoSolicitudes.Errores("archivo", errorArchivo.Mensaje));
        }

        var espacio = await _espacios.ObtenerParaReservaAsync(comando.EspacioId, cancellationToken);

        if (espacio is null)
        {
            return Resultado<SolicitudRadicada>.Fallo(TipoFallo.NoEncontrado, MensajesEspacio.EspacioNoEncontrado);
        }

        if (espacio.Estado != EstadoUnidad.Activo)
        {
            return Resultado<SolicitudRadicada>.Fallo(TipoFallo.Conflicto, MensajesSolicitud.EspacioNoDisponible);
        }

        if (espacio.TipoAprobacion == TipoAprobacion.Automatica)
        {
            return Resultado<SolicitudRadicada>.Fallo(TipoFallo.Conflicto, MensajesSolicitud.EspacioNoSolicitable);
        }

        if (espacio.TipoModalidadPrestamo != TipoModalidadPrestamo.AreaCompleta)
        {
            return Resultado<SolicitudRadicada>.Fallo(TipoFallo.Conflicto, MensajesSolicitud.ModalidadIncompatible);
        }

        try
        {
            var zona = _reservas.ObtenerZonaHoraria();
            var ahora = _reloj.Ahora;

            var franja = ValidacionesSolicitud.CrearFranja(
                comando,
                _reservas.ObtenerPolitica(),
                zona,
                ahora,
                _solicitudes.AntelacionMinimaMinutos);

            var calendario = await _calendario.ObtenerCalendarioAsync(
                DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(franja.Inicio, zona).DateTime),
                DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(franja.Fin, zona).DateTime),
                zona,
                cancellationToken);

            if (ValidacionesSolicitud.MensajeCalendario(franja, calendario, zona) is { } motivo)
            {
                return Resultado<SolicitudRadicada>.Fallo(TipoFallo.Conflicto, motivo);
            }

            var activas = await _consulta.ContarActivasAsync(solicitante, cancellationToken);

            if (activas >= _solicitudes.MaxSolicitudesActivasPorUsuario)
            {
                return Resultado<SolicitudRadicada>.Fallo(
                    TipoFallo.Conflicto,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        MensajesSolicitud.LimiteSolicitudes,
                        _solicitudes.MaxSolicitudesActivasPorUsuario));
            }

            return await GuardarAsync(comando, espacio, franja, solicitante, ahora, cancellationToken);
        }
        catch (DomainException excepcion)
        {
            return Resultado<SolicitudRadicada>.Fallo(
                TipoFallo.Validacion,
                excepcion.Message,
                MapeoSolicitudes.Errores(MapeoSolicitudes.CampoDe(excepcion.Message), excepcion.Message));
        }
    }

    private async Task<Resultado<SolicitudRadicada>> GuardarAsync(
        ComandoRadicarSolicitud comando,
        EspacioParaReserva espacio,
        FranjaHoraria franja,
        Guid solicitante,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var solicitudId = Guid.NewGuid();

        var guardado = await _documentos.GuardarPdfAsync(
            solicitudId,
            comando.Archivo!,
            comando.NombreArchivo!,
            comando.LongitudArchivo,
            cancellationToken);

        if (!guardado.Exitoso || guardado.Valor is null)
        {
            var mensaje = guardado.Mensaje ?? MensajesSolicitud.ArchivoNoValido;

            return Resultado<SolicitudRadicada>.Fallo(
                guardado.TipoFallo,
                mensaje,
                MapeoSolicitudes.Errores("archivo", mensaje));
        }

        var solicitud = SolicitudPrestamo.Radicar(
            solicitante,
            espacio.Id,
            franja,
            comando.Materia,
            comando.DocenteResponsable,
            comando.Justificacion,
            guardado.Valor,
            comando.NombreArchivo!,
            comando.LongitudArchivo,
            ahora,
            solicitudId);

        await _repositorio.AgregarAsync(solicitud, cancellationToken);

        if (!await _repositorio.GuardarCambiosAsync(cancellationToken))
        {
            // Borrado compensatorio: la solicitud no existe y el archivo no debe quedar huérfano.
            await _documentos.EliminarAsync(guardado.Valor, cancellationToken);

            return Resultado<SolicitudRadicada>.Fallo(
                TipoFallo.Conflicto,
                "No fue posible registrar la solicitud. Intente nuevamente.");
        }

        return Resultado<SolicitudRadicada>.Exito(new SolicitudRadicada(
            solicitud.Id,
            solicitud.EspacioId,
            solicitud.Estado,
            solicitud.Inicio,
            solicitud.Fin,
            solicitud.RadicadaEn,
            solicitud.ArchivoNombreOriginal,
            solicitud.ArchivoTamanoBytes));
    }
}
