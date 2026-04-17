using System.Text.Json.Serialization;

namespace Chatbot.Models;

public class ChatbotData
{
    public string Id         { get; set; } = string.Empty;
    public string Name              { get; set; } = string.Empty;
    public string Speciality        { get; set; } = string.Empty;
    public string Description       { get; set; } = string.Empty;
    public string EndSessionMessage { get; set; } = string.Empty;
    public string UnknownMessage    { get; set; } = string.Empty;
    public string ByeMessage        { get; set; } = string.Empty;
    [JsonPropertyName("qa_pairs")]
    public List<QAPair> QaPairs     { get; set; } = new();
}

public class QAPair
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Deep { get; set; }
    public string? Category { get; set; }
}
