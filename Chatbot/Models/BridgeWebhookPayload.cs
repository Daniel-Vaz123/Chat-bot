namespace Chatbot.Models;

public class BridgeWebhookPayload
{
    public string From { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsAudio { get; set; }
}
