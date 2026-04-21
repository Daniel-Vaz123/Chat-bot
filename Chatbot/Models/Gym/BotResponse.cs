namespace Chatbot.Models.Gym;

/// <summary>
/// Respuesta del bot hacia el canal de mensajería.
/// Siempre se retorna un BotResponse no nulo — nunca se propagan excepciones al caller.
/// </summary>
public class BotResponse
{
    /// <summary>Texto del mensaje a enviar al usuario. Nunca nulo ni vacío.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Indica si la respuesta es un error técnico (para logging/alertas).</summary>
    public bool IsError { get; set; }

    /// <summary>Metadatos opcionales para el canal (ej. botones, media, quick replies).</summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>Crea una respuesta exitosa con el mensaje dado.</summary>
    public static BotResponse Ok(string message) =>
        new() { Message = message, IsError = false };

    /// <summary>Crea una respuesta de error con mensaje genérico para el usuario.</summary>
    public static BotResponse Error(string message) =>
        new() { Message = message, IsError = true };
}
