using Chatbot.Models;
using Chatbot.Services;
using Chatbot.Services.Gym;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Chatbot.Controllers;

/// <summary>
/// Recibe notificaciones de Twilio WhatsApp y las enruta al <see cref="IGymConversationRouter"/>.
/// Siempre retorna HTTP 200 para evitar reintentos automáticos de Twilio.
/// </summary>
[ApiController]
[Route("api/whatsapp")]
public sealed class WhatsAppController : ControllerBase
{
    private const string AudioFallback = "[audio no reconocido]";
    private const string TwimlEmptyResponse = "<Response/>";

    private readonly IGymConversationRouter _router;
    private readonly IChannelAdapter _channelAdapter;
    private readonly IAudioTranscriptionService _transcriptionService;
    private readonly TwilioSettings _twilioSettings;
    private readonly ILogger<WhatsAppController> _logger;

    public WhatsAppController(
        IGymConversationRouter router,
        IChannelAdapter channelAdapter,
        IAudioTranscriptionService transcriptionService,
        IOptions<TwilioSettings> twilioSettings,
        ILogger<WhatsAppController> logger)
    {
        _router = router;
        _channelAdapter = channelAdapter;
        _transcriptionService = transcriptionService;
        _twilioSettings = twilioSettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Endpoint webhook que recibe mensajes de WhatsApp desde Twilio.
    /// Acepta application/x-www-form-urlencoded.
    /// </summary>
    [HttpPost]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> ReceiveWebhook([FromForm] TwilioWebhookPayload payload)
    {
        // Validar que el campo From no esté vacío
        if (string.IsNullOrWhiteSpace(payload.From))
        {
            _logger.LogWarning("Webhook recibido sin campo 'From'. Ignorando.");
            return TwimlOk();
        }

        var userId = payload.From;
        var isAudio = payload.NumMedia > 0 &&
                      !string.IsNullOrEmpty(payload.MediaContentType0) &&
                      payload.MediaContentType0.StartsWith("audio/", StringComparison.OrdinalIgnoreCase);

        _logger.LogInformation(
            "Webhook recibido. UserId: {UserId}, Tipo: {MessageType}",
            userId, isAudio ? "audio" : "texto");

        try
        {
            // Resolver el texto del mensaje (directo o transcrito)
            var resolvedText = await ResolveMessageTextAsync(payload, userId, isAudio);

            // Enrutar al motor de conversación
            var botResponse = await _router.RouteMessageAsync(userId, resolvedText);

            // Enviar la respuesta al usuario vía Twilio
            try
            {
                await _channelAdapter.SendMessageAsync(userId, botResponse.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error al enviar respuesta a {UserId} vía Twilio.", userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error no controlado procesando webhook para {UserId}.", userId);
        }

        // Siempre retornar HTTP 200 con TwiML vacío para evitar reintentos de Twilio
        return TwimlOk();
    }

    private async Task<string> ResolveMessageTextAsync(
        TwilioWebhookPayload payload, string userId, bool isAudio)
    {
        if (!isAudio)
        {
            return payload.Body ?? string.Empty;
        }

        // Mensaje de audio: transcribir con Vosk
        try
        {
            var transcribed = await _transcriptionService.TranscribeAsync(
                audioUrl: payload.MediaUrl0!,
                authUser: _twilioSettings.AccountSid,
                authPassword: _twilioSettings.AuthToken);

            if (string.IsNullOrWhiteSpace(transcribed))
            {
                _logger.LogInformation(
                    "Transcripción vacía para {UserId}. Usando fallback.", userId);
                return AudioFallback;
            }

            return transcribed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error al transcribir audio para {UserId}. Usando fallback.", userId);
            return AudioFallback;
        }
    }

    /// <summary>Retorna HTTP 200 con un body TwiML vacío.</summary>
    private ContentResult TwimlOk() =>
        Content(TwimlEmptyResponse, "text/xml");
}
