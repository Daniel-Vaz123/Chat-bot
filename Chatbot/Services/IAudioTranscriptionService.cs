namespace Chatbot.Services;

/// <summary>
/// Abstrae la transcripción de audio a texto.
/// </summary>
public interface IAudioTranscriptionService
{
    /// <summary>
    /// Descarga el audio desde <paramref name="audioUrl"/> usando las credenciales
    /// proporcionadas y lo transcribe localmente con Vosk.
    /// </summary>
    /// <param name="audioUrl">URL del archivo de audio remoto.</param>
    /// <param name="authUser">Usuario para autenticación HTTP Basic.</param>
    /// <param name="authPassword">Contraseña para autenticación HTTP Basic.</param>
    /// <returns>Texto transcrito. Puede ser string vacío si no se reconoció nada.</returns>
    /// <exception cref="InvalidOperationException">Si el audio no puede descargarse.</exception>
    Task<string> TranscribeAsync(string audioUrl, string authUser, string authPassword);

    /// <summary>
    /// Transcribe audio ya descargado (p. ej. nota de voz del bridge de WhatsApp en Ogg/Opus).
    /// </summary>
    Task<string> TranscribeFromBytesAsync(byte[] audioBytes, CancellationToken cancellationToken = default);
}
