using Chatbot.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace Chatbot.Services.Gym;

/// <summary>
/// Implementación de <see cref="IChannelAdapter"/> que envía mensajes de WhatsApp
/// a través del SDK oficial de Twilio.
/// Registrar como Scoped.
/// </summary>
public sealed class TwilioWhatsAppAdapter : IChannelAdapter
{
    private readonly TwilioSettings _settings;
    private readonly ILogger<TwilioWhatsAppAdapter> _logger;

    public TwilioWhatsAppAdapter(
        IOptions<TwilioSettings> settings,
        ILogger<TwilioWhatsAppAdapter> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task SendMessageAsync(string userId, string message)
    {
        _logger.LogDebug(
            "Enviando mensaje a {UserId} (longitud: {Length} caracteres).",
            userId, message.Length);

        // Inicializar el cliente Twilio con las credenciales de configuración
        TwilioClient.Init(_settings.AccountSid, _settings.AuthToken);

        // El número de origen debe tener el prefijo whatsapp:
        // El userId ya llega con el prefijo whatsapp: desde el webhook de Twilio
        var messageResource = await MessageResource.CreateAsync(
            from: new PhoneNumber($"whatsapp:{_settings.WhatsAppNumber}"),
            to: new PhoneNumber(userId),
            body: message
        );

        _logger.LogDebug(
            "Mensaje enviado a {UserId}. SID: {MessageSid}",
            userId, messageResource.Sid);
    }
}
