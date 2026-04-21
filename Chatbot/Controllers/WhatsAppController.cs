using Chatbot.Models;
using Chatbot.Services;
using Chatbot.Services.Gym;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

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

    private readonly ChatbotService _chatbotService;
    private readonly IChannelAdapter _channelAdapter;
    private readonly IAudioTranscriptionService _transcriptionService;
    private readonly INotificationResources _notificationResources;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<WhatsAppController> _logger;

    private const string CacheKeyWelcomePrefix = "whatsapp_welcome_sent:";

    public WhatsAppController(
        ChatbotService chatbotService,
        IChannelAdapter channelAdapter,
        IAudioTranscriptionService transcriptionService,
        INotificationResources notificationResources,
        IMemoryCache memoryCache,
        ILogger<WhatsAppController> logger)
    {
        _chatbotService = chatbotService;
        _channelAdapter = channelAdapter;
        _transcriptionService = transcriptionService;
        _notificationResources = notificationResources;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    /// <summary>
    /// Endpoint webhook que recibe mensajes de WhatsApp desde Twilio.
    /// Acepta application/x-www-form-urlencoded.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    public async Task<IActionResult> ReceiveWebhook([FromBody] BridgeWebhookPayload payload)
    {
        // Validar que el campo From no esté vacío
        if (string.IsNullOrWhiteSpace(payload.From))
        {
            _logger.LogWarning("Webhook recibido sin campo 'From'. Ignorando.");
            return TwimlOk();
        }

        var userId = payload.From;
        var isAudio = payload.IsAudio;

        _logger.LogInformation(
            "Webhook recibido de Node Bridge. UserId: {UserId}, Tipo: {MessageType}",
            userId, isAudio ? "audio" : "texto");

        try
        {
            // OBTENER EL TEXTO FINAL A PROCESAR (Audio o Texto)
            var textToProcess = await ResolveMessageTextAsync(payload, userId, isAudio);

            if (ShouldSendOpeningMessage(userId, textToProcess))
            {
                var opening = _notificationResources.GetWhatsAppOpeningMessage();
                await _channelAdapter.SendMessageAsync(userId, opening);
                _memoryCache.Set(
                    CacheKeyWelcomePrefix + userId,
                    true,
                    new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromDays(7) });
                return TwimlOk();
            }

            // PASAR DIRECTO A LA INTELIGENCIA (Búsqueda Vectorial S3 + DeepSeek API)
            var aiResponse = await _chatbotService.AskQuestionAsync(textToProcess);

            if (string.IsNullOrWhiteSpace(aiResponse))
            {
                aiResponse = "No pude generar una respuesta. Intenta de nuevo o reformula la pregunta.";
            }

            // RESPONDER
            await _channelAdapter.SendMessageAsync(userId, aiResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error no controlado procesando webhook para {UserId}.", userId);
            try
            {
                await _channelAdapter.SendMessageAsync(
                    userId,
                    "Tuve un problema al procesar tu mensaje. Revisa que el backend esté corriendo y vuelve a intentar. Si sigue fallando, avísale al administrador.");
            }
            catch (Exception sendEx)
            {
                _logger.LogError(sendEx, "No se pudo enviar mensaje de error al usuario {UserId}.", userId);
            }
        }

        // Siempre retornar HTTP 200 con TwiML vacío para evitar reintentos de Twilio
        return TwimlOk();
    }

    /// <summary>
    /// Saludo vacío o palabras tipo hola/menú: saludo + temas frecuentes (sin RAG).
    /// Si ya enviamos ese bloque recientemente, un nuevo "hola" pasa a RAG.
    /// </summary>
    private bool ShouldSendOpeningMessage(string userId, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var t = text.Trim().ToLowerInvariant();

        if (!IsMenuOrGreetingTrigger(t))
        {
            return false;
        }

        // Evita repetir el mismo texto largo si vuelve a saludar en pocos días
        return !_memoryCache.TryGetValue(CacheKeyWelcomePrefix + userId, out _);
    }

    private static bool IsMenuOrGreetingTrigger(string normalizedLower)
    {
        string[] exact = ["hola", "holaa", "holas", "hey", "hi", "hello", "saludos", "buenos", "buenas",
            "menu", "menú", "inicio", "ayuda", "info", "información", "informacion", "gracias", "ok", "vale"];

        if (exact.Contains(normalizedLower))
        {
            return true;
        }

        string[] prefixes =
        [
            "hola ", "hola,", "hola!", "buenos ", "buenas ", "qué tal", "que tal", "buen día", "buen dia",
            "buenas tardes", "buenas noches", "buen día", "menu ", "menú ", "ayuda "
        ];

        foreach (var p in prefixes)
        {
            if (normalizedLower.StartsWith(p, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<string> ResolveMessageTextAsync(
        BridgeWebhookPayload payload, string userId, bool isAudio)
    {
        if (!isAudio)
        {
            return payload.Body ?? string.Empty;
        }

        // Para el puente Node, el audio llegaría diferido o en Base64.
        // Por simplicidad temporal, como es proyecto local usando Node.js 
        // pasamos el mensaje que haya podido llegar como fallback
        
        return AudioFallback;
    }

    /// <summary>Retorna HTTP 200 con un body TwiML vacío.</summary>
    private ContentResult TwimlOk() =>
        Content(TwimlEmptyResponse, "text/xml");
}
