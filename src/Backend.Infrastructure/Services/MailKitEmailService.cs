using Backend.Application.Common;
using Backend.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Backend.Infrastructure.Services;

/// <summary>
/// Envío de notificaciones con MailKit/MimeKit. El mensaje se compone con la plantilla de la
/// capa de aplicación y nunca depende del ciclo de la petición HTTP.
/// </summary>
public sealed class MailKitEmailService : IEmailService
{
    private readonly OpcionesSmtp _opciones;

    /// <summary>Inicializa el servicio con la configuración SMTP.</summary>
    public MailKitEmailService(OpcionesSmtp opciones)
    {
        _opciones = opciones ?? throw new ArgumentNullException(nameof(opciones));
    }

    /// <inheritdoc />
    public async Task<ResultadoEnvioCorreo> EnviarAsync(
        string destinatario,
        string asunto,
        string cuerpoHtml,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destinatario))
        {
            return new ResultadoEnvioCorreo(false, "El destinatario del correo es obligatorio.");
        }

        try
        {
            var mensaje = new MimeMessage();
            mensaje.From.Add(new MailboxAddress(_opciones.RemitenteNombre, _opciones.RemitenteCorreo));
            mensaje.To.Add(MailboxAddress.Parse(destinatario));
            mensaje.Subject = asunto;
            mensaje.Body = new TextPart("html") { Text = cuerpoHtml };

            using var cliente = new SmtpClient();

            await cliente.ConnectAsync(
                _opciones.Host,
                _opciones.Puerto,
                _opciones.UsarSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.None,
                cancellationToken);

            if (_opciones.RequiereAutenticacion)
            {
                await cliente.AuthenticateAsync(_opciones.Usuario, _opciones.Contrasena, cancellationToken);
            }

            await cliente.SendAsync(mensaje, cancellationToken);
            await cliente.DisconnectAsync(quit: true, cancellationToken);

            return new ResultadoEnvioCorreo(true, null);
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            return new ResultadoEnvioCorreo(false, excepcion.Message);
        }
    }
}
