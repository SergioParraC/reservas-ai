namespace Backend.Domain.Exceptions;

/// <summary>
/// Constantes de los mensajes de negocio del dominio de espacios.
/// Se centralizan para que la API y las pruebas compartan el mismo texto.
/// </summary>
public static class MensajesEspacio
{
    /// <summary>El nombre del área es obligatorio.</summary>
    public const string NombreRequerido = "El nombre del área es obligatorio.";

    /// <summary>El nombre del área excede la longitud admitida.</summary>
    public const string NombreDemasiadoLargo = "El nombre del área no puede superar los 100 caracteres.";

    /// <summary>La ubicación del área es obligatoria.</summary>
    public const string UbicacionRequerida = "La ubicación del área es obligatoria.";

    /// <summary>La ubicación del área excede la longitud admitida.</summary>
    public const string UbicacionDemasiadoLarga = "La ubicación del área no puede superar los 150 caracteres.";

    /// <summary>La categoría recibida no pertenece al enum oficial.</summary>
    public const string CategoriaInvalida = "La categoría del área no es válida.";

    /// <summary>El tipo de aprobación recibido no pertenece al enum oficial.</summary>
    public const string TipoAprobacionInvalido = "El tipo de aprobación del área no es válido.";

    /// <summary>El estado recibido no pertenece al enum oficial.</summary>
    public const string EstadoInvalido = "El estado de la unidad no es válido.";

    /// <summary>El aforo debe ser al menos uno.</summary>
    public const string AforoMinimo = "El aforo debe ser mayor o igual a 1.";

    /// <summary>El aforo supera el máximo institucional configurado.</summary>
    public const string AforoMaximoSuperado = "El aforo no puede superar el máximo institucional de {0} personas.";

    /// <summary>El aforo no puede ser inferior a la cantidad de puestos activos.</summary>
    public const string AforoMenorQuePuestos = "El aforo no puede ser menor que los {0} puestos activos del área.";

    /// <summary>La cantidad de puestos activos no puede superar el aforo del área.</summary>
    public const string AforoPuestosExcedido = "El área ya alcanzó su aforo de {0} puestos activos.";

    /// <summary>El identificador visible del puesto es obligatorio.</summary>
    public const string PuestoIdentificadorRequerido = "El identificador del puesto es obligatorio.";

    /// <summary>El identificador visible del puesto excede la longitud admitida.</summary>
    public const string PuestoIdentificadorDemasiadoLargo = "El identificador del puesto no puede superar los 50 caracteres.";

    /// <summary>Ya existe un puesto con el mismo identificador en el área.</summary>
    public const string PuestoIdentificadorDuplicado = "Ya existe un puesto con el identificador '{0}' en el área.";

    /// <summary>El puesto indicado no pertenece al área.</summary>
    public const string PuestoInexistente = "El puesto indicado no pertenece al área.";

    /// <summary>La descripción del equipo excede la longitud admitida.</summary>
    public const string DescripcionEquipoDemasiadoLarga = "La descripción del equipo no puede superar los 150 caracteres.";

    /// <summary>Ya existe un área con el mismo nombre en la misma ubicación.</summary>
    public const string NombreUbicacionDuplicado = "Ya existe un área con el mismo nombre en esa ubicación.";

    /// <summary>El código QR recibido no cumple el formato oficial.</summary>
    public const string CodigoQrInvalido = "El código QR no cumple el formato institucional.";

    /// <summary>El área solicitada no existe.</summary>
    public const string EspacioNoEncontrado = "El área solicitada no existe.";

    /// <summary>El área no se presta como área completa.</summary>
    public const string AreaNoReservable = "El área no admite reserva de área completa.";

    /// <summary>La modalidad de préstamo recibida no pertenece al enum oficial.</summary>
    public const string ModalidadInvalida = "La modalidad de préstamo del área no es válida.";

    /// <summary>El elemento de préstamo recibido no pertenece al enum oficial.</summary>
    public const string ElementoInvalido = "El elemento de préstamo del área no es válido.";

    /// <summary>La combinación de modalidad y elemento no es coherente.</summary>
    public const string CombinacionPrestamoInvalida = "La combinación de modalidad y elemento de préstamo no es válida para el área.";

    /// <summary>La modalidad no puede cambiarse porque el área tiene ocupación futura.</summary>
    public const string ModalidadConOcupacion = "No es posible cambiar la modalidad de préstamo: el área tiene ocupación futura.";

    /// <summary>La modalidad del área no admite el tipo de reserva solicitado.</summary>
    public const string ModalidadIncompatible = "La modalidad de préstamo del área no admite este tipo de reserva.";
}
