using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Models;

/// <summary>
/// Representa el payload de una notificación entrante de Twilio WhatsApp.
/// Los campos se mapean desde application/x-www-form-urlencoded.
/// </summary>
public class TwilioWebhookPayload
{
    [FromForm(Name = "From")]
    public string From { get; set; } = string.Empty;

    [FromForm(Name = "Body")]
    public string Body { get; set; } = string.Empty;

    [FromForm(Name = "NumMedia")]
    public int NumMedia { get; set; }

    [FromForm(Name = "MediaUrl0")]
    public string? MediaUrl0 { get; set; }

    [FromForm(Name = "MediaContentType0")]
    public string? MediaContentType0 { get; set; }
}
