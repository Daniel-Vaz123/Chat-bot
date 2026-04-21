namespace Chatbot.Models;

/// <summary>
/// Configuración de Vosk leída desde IConfiguration (sección "Vosk").
/// ModelPath apunta al directorio del modelo descargado localmente.
/// </summary>
public class VoskSettings
{
    public string ModelPath { get; set; } = string.Empty;
}
