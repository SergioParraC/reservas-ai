using Backend.Domain.Enums;
using Backend.Domain.Exceptions;
using Backend.Domain.ValueObjects;

namespace Backend.Domain.Entities;

/// <summary>
/// Área física del campus. Es la raíz del agregado que concentra las invariantes de aforo,
/// estados y puestos, y el código QR inmutable que identifica la unidad.
/// </summary>
public sealed class Espacio
{
    /// <summary>Longitud máxima del nombre del área.</summary>
    public const int LongitudMaximaNombre = 100;

    /// <summary>Longitud máxima de la ubicación del área.</summary>
    public const int LongitudMaximaUbicacion = 150;

    /// <summary>Longitud máxima del identificador visible de un puesto.</summary>
    public const int LongitudMaximaIdentificadorPuesto = 50;

    /// <summary>Longitud máxima de la descripción del equipo de un puesto.</summary>
    public const int LongitudMaximaDescripcionEquipo = 150;

    private readonly List<Puesto> _puestos = [];

    private Espacio()
    {
    }

    private Espacio(
        Guid id,
        string nombre,
        string ubicacion,
        CategoriaEspacio categoria,
        int aforo,
        TipoAprobacion tipoAprobacion,
        CodigoQR codigoQr,
        DateTimeOffset creadoEn)
    {
        Id = id;
        Nombre = nombre;
        Ubicacion = ubicacion;
        Categoria = categoria;
        Aforo = aforo;
        TipoAprobacion = tipoAprobacion;
        CodigoQR = codigoQr;
        Estado = EstadoUnidad.Activo;
        CreadoEn = creadoEn;
        ActualizadoEn = creadoEn;
    }

    /// <summary>Identificador interno del área.</summary>
    public Guid Id { get; private set; }

    /// <summary>Nombre visible del área.</summary>
    public string Nombre { get; private set; } = string.Empty;

    /// <summary>Ubicación física (bloque, piso, referencia).</summary>
    public string Ubicacion { get; private set; } = string.Empty;

    /// <summary>Categoría funcional del área.</summary>
    public CategoriaEspacio Categoria { get; private set; }

    /// <summary>Aforo máximo de personas.</summary>
    public int Aforo { get; private set; }

    /// <summary>Responsable de aprobar las reservas del área.</summary>
    public TipoAprobacion TipoAprobacion { get; private set; }

    /// <summary>Estado del área; la baja es lógica.</summary>
    public EstadoUnidad Estado { get; private set; }

    /// <summary>Código QR inmutable asignado en el alta del área.</summary>
    public CodigoQR CodigoQR { get; private set; } = null!;

    /// <summary>Ruta relativa de la fotografía en el almacén; nunca la ruta física.</summary>
    public string? FotografiaRuta { get; private set; }

    /// <summary>Momento del alta.</summary>
    public DateTimeOffset CreadoEn { get; private set; }

    /// <summary>Momento de la última modificación.</summary>
    public DateTimeOffset ActualizadoEn { get; private set; }

    /// <summary>Token de concurrencia optimista gestionado por la base de datos.</summary>
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>Puestos de cómputo del área; se mutan solo con métodos del agregado.</summary>
    public IReadOnlyCollection<Puesto> Puestos => _puestos;

    /// <summary>Cantidad de puestos actualmente activos.</summary>
    public int PuestosActivos => _puestos.Count(puesto => puesto.Estado == EstadoUnidad.Activo);

    /// <summary>
    /// Crea un área activa con su código QR. El máximo de aforo es una política institucional
    /// recibida como parámetro para no acoplar el dominio a la configuración.
    /// </summary>
    public static Espacio Crear(
        string nombre,
        string ubicacion,
        CategoriaEspacio categoria,
        int aforo,
        int aforoMaximo,
        TipoAprobacion tipoAprobacion,
        DateTimeOffset creadoEn,
        CodigoQR? codigoQr = null)
    {
        var nombreNormalizado = NormalizarNombre(nombre);
        var ubicacionNormalizada = NormalizarUbicacion(ubicacion);

        ValidarCategoria(categoria);
        ValidarTipoAprobacion(tipoAprobacion);
        ValidarAforo(aforo, aforoMaximo);

        return new Espacio(
            Guid.NewGuid(),
            nombreNormalizado,
            ubicacionNormalizada,
            categoria,
            aforo,
            tipoAprobacion,
            codigoQr ?? CodigoQR.Generar(),
            creadoEn);
    }

    /// <summary>Actualiza los datos descriptivos, el aforo y el estado del área.</summary>
    public void Actualizar(
        string nombre,
        string ubicacion,
        CategoriaEspacio categoria,
        TipoAprobacion tipoAprobacion,
        EstadoUnidad estado,
        int aforo,
        int aforoMaximo,
        DateTimeOffset momento)
    {
        Nombre = NormalizarNombre(nombre);
        Ubicacion = NormalizarUbicacion(ubicacion);

        ValidarCategoria(categoria);
        ValidarTipoAprobacion(tipoAprobacion);
        ValidarEstado(estado);
        ValidarAforo(aforo, aforoMaximo);
        ValidarAforoContraPuestos(aforo);

        Categoria = categoria;
        TipoAprobacion = tipoAprobacion;
        Estado = estado;
        Aforo = aforo;
        ActualizadoEn = momento;
    }

    /// <summary>Cambia el aforo respetando el máximo institucional y los puestos activos.</summary>
    public void CambiarAforo(int aforo, int aforoMaximo, DateTimeOffset momento)
    {
        ValidarAforo(aforo, aforoMaximo);
        ValidarAforoContraPuestos(aforo);

        Aforo = aforo;
        ActualizadoEn = momento;
    }

    /// <summary>Agrega un puesto al área validando el aforo y la unicidad del identificador.</summary>
    public Puesto AgregarPuesto(
        string identificador,
        string? descripcionEquipo,
        DateTimeOffset momento,
        CodigoQR? codigoQr = null)
    {
        if (PuestosActivos >= Aforo)
        {
            throw new ConflictoException(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                MensajesEspacio.AforoPuestosExcedido,
                Aforo));
        }

        var identificadorNormalizado = NormalizarIdentificador(identificador);
        var descripcionNormalizada = NormalizarDescripcion(descripcionEquipo);

        if (_puestos.Any(puesto => string.Equals(puesto.Identificador, identificadorNormalizado, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictoException(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                MensajesEspacio.PuestoIdentificadorDuplicado,
                identificadorNormalizado));
        }

        var puesto = Puesto.Crear(
            Id,
            identificadorNormalizado,
            descripcionNormalizada,
            codigoQr ?? CodigoQR.Generar(),
            momento);

        _puestos.Add(puesto);
        ActualizadoEn = momento;

        return puesto;
    }

    /// <summary>Actualiza un puesto existente y revalida el aforo si se reactiva.</summary>
    public Puesto ActualizarPuesto(
        Guid puestoId,
        string identificador,
        string? descripcionEquipo,
        EstadoUnidad estado,
        DateTimeOffset momento)
    {
        var puesto = ObtenerPuesto(puestoId)
            ?? throw new DomainException(MensajesEspacio.PuestoInexistente);

        var identificadorNormalizado = NormalizarIdentificador(identificador);
        var descripcionNormalizada = NormalizarDescripcion(descripcionEquipo);

        ValidarEstado(estado);

        if (_puestos.Any(candidato =>
                candidato.Id != puestoId
                && string.Equals(candidato.Identificador, identificadorNormalizado, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictoException(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                MensajesEspacio.PuestoIdentificadorDuplicado,
                identificadorNormalizado));
        }

        if (puesto.Estado == EstadoUnidad.Inactivo
            && estado == EstadoUnidad.Activo
            && PuestosActivos >= Aforo)
        {
            throw new ConflictoException(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                MensajesEspacio.AforoPuestosExcedido,
                Aforo));
        }

        puesto.Actualizar(identificadorNormalizado, descripcionNormalizada, estado);
        ActualizadoEn = momento;

        return puesto;
    }

    /// <summary>Da de baja lógica un puesto del área.</summary>
    public Puesto DesactivarPuesto(Guid puestoId, DateTimeOffset momento)
    {
        var puesto = ObtenerPuesto(puestoId)
            ?? throw new DomainException(MensajesEspacio.PuestoInexistente);

        puesto.Desactivar();
        ActualizadoEn = momento;

        return puesto;
    }

    /// <summary>Busca un puesto del área por su identificador interno.</summary>
    public Puesto? ObtenerPuesto(Guid puestoId) => _puestos.FirstOrDefault(puesto => puesto.Id == puestoId);

    /// <summary>Da de baja lógica el área conservando su código QR.</summary>
    public void Desactivar(DateTimeOffset momento)
    {
        Estado = EstadoUnidad.Inactivo;
        ActualizadoEn = momento;
    }

    /// <summary>Reactiva el área sin alterar su código QR.</summary>
    public void Activar(DateTimeOffset momento)
    {
        Estado = EstadoUnidad.Activo;
        ActualizadoEn = momento;
    }

    /// <summary>Asocia la ruta relativa de la fotografía del área.</summary>
    public void EstablecerFotografia(string rutaRelativa, DateTimeOffset momento)
    {
        if (string.IsNullOrWhiteSpace(rutaRelativa))
        {
            throw new DomainException("La ruta de la fotografía es obligatoria.");
        }

        FotografiaRuta = rutaRelativa.Trim();
        ActualizadoEn = momento;
    }

    /// <summary>Retira la ruta de la fotografía del área.</summary>
    public void QuitarFotografia(DateTimeOffset momento)
    {
        FotografiaRuta = null;
        ActualizadoEn = momento;
    }

    private static string NormalizarNombre(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new DomainException(MensajesEspacio.NombreRequerido);
        }

        var normalizado = nombre.Trim();

        if (normalizado.Length > LongitudMaximaNombre)
        {
            throw new DomainException(MensajesEspacio.NombreDemasiadoLargo);
        }

        return normalizado;
    }

    private static string NormalizarUbicacion(string ubicacion)
    {
        if (string.IsNullOrWhiteSpace(ubicacion))
        {
            throw new DomainException(MensajesEspacio.UbicacionRequerida);
        }

        var normalizada = ubicacion.Trim();

        if (normalizada.Length > LongitudMaximaUbicacion)
        {
            throw new DomainException(MensajesEspacio.UbicacionDemasiadoLarga);
        }

        return normalizada;
    }

    private static string NormalizarIdentificador(string identificador)
    {
        if (string.IsNullOrWhiteSpace(identificador))
        {
            throw new DomainException(MensajesEspacio.PuestoIdentificadorRequerido);
        }

        var normalizado = identificador.Trim();

        if (normalizado.Length > LongitudMaximaIdentificadorPuesto)
        {
            throw new DomainException(MensajesEspacio.PuestoIdentificadorDemasiadoLargo);
        }

        return normalizado;
    }

    private static string? NormalizarDescripcion(string? descripcion)
    {
        if (string.IsNullOrWhiteSpace(descripcion))
        {
            return null;
        }

        var normalizada = descripcion.Trim();

        if (normalizada.Length > LongitudMaximaDescripcionEquipo)
        {
            throw new DomainException(MensajesEspacio.DescripcionEquipoDemasiadoLarga);
        }

        return normalizada;
    }

    private static void ValidarCategoria(CategoriaEspacio categoria)
    {
        if (!Enum.IsDefined(categoria))
        {
            throw new DomainException(MensajesEspacio.CategoriaInvalida);
        }
    }

    private static void ValidarTipoAprobacion(TipoAprobacion tipoAprobacion)
    {
        if (!Enum.IsDefined(tipoAprobacion))
        {
            throw new DomainException(MensajesEspacio.TipoAprobacionInvalido);
        }
    }

    private static void ValidarEstado(EstadoUnidad estado)
    {
        if (!Enum.IsDefined(estado))
        {
            throw new DomainException(MensajesEspacio.EstadoInvalido);
        }
    }

    private static void ValidarAforo(int aforo, int aforoMaximo)
    {
        if (aforo < 1)
        {
            throw new DomainException(MensajesEspacio.AforoMinimo);
        }

        if (aforo > aforoMaximo)
        {
            throw new DomainException(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                MensajesEspacio.AforoMaximoSuperado,
                aforoMaximo));
        }
    }

    private void ValidarAforoContraPuestos(int aforo)
    {
        if (aforo < PuestosActivos)
        {
            throw new ConflictoException(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                MensajesEspacio.AforoMenorQuePuestos,
                PuestosActivos));
        }
    }
}

