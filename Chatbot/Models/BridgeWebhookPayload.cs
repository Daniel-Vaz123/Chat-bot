using System.Text.Json.Serialization;

namespace Chatbot.Models;

public class BridgeWebhookPayload
{
    public string From { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsAudio { get; set; }

    /// <summary>Audio en base64 (sin prefijo data:), enviado por el bridge Node para notas de voz.</summary>
    [JsonPropertyName("audioBase64")]
    public string? AudioBase64 { get; set; }

    [JsonPropertyName("audioMimeType")]
    public string? AudioMimeType { get; set; }
}
