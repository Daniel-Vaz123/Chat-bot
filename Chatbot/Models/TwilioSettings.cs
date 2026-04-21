namespace Chatbot.Models;

/// <summary>
/// Configuración de Twilio leída desde IConfiguration (sección "Twilio").
/// No contiene valores por defecto con credenciales reales.
/// </summary>
public class TwilioSettings
{
    public string AccountSid { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public string WhatsAppNumber { get; set; } = string.Empty;
}
