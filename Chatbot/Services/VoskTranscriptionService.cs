using System.Net.Http.Headers;
using System.Text.Json;
using Chatbot.Models;
using Concentus.Structs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vosk;

namespace Chatbot.Services;

/// <summary>
/// Transcribe notas de voz de WhatsApp (Ogg/Opus) a texto usando Vosk offline.
/// Registrar como Singleton — el modelo Vosk es costoso de cargar (cientos de MB).
/// </summary>
public sealed class VoskTranscriptionService : IAudioTranscriptionService, IDisposable
{
    // Vosk 0.3.38 expone la clase principal como "Model"
    private readonly Model _model;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VoskTranscriptionService> _logger;
    private bool _disposed;

    public VoskTranscriptionService(
        IOptions<VoskSettings> settings,
        IHttpClientFactory httpClientFactory,
        ILogger<VoskTranscriptionService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        var modelPath = settings.Value.ModelPath;

        if (string.IsNullOrWhiteSpace(modelPath) || !Directory.Exists(modelPath))
        {
            throw new InvalidOperationException(
                $"El directorio del modelo Vosk no existe o no está configurado: '{modelPath}'. " +
                "Descarga un modelo desde https://alphacephei.com/vosk/models y configura Vosk:ModelPath.");
        }

        _logger.LogInformation("Cargando modelo Vosk desde: {ModelPath}", modelPath);
        Vosk.Vosk.SetLogLevel(-1); // Silenciar logs verbosos de Vosk
        _model = new Model(modelPath);
        _logger.LogInformation("Modelo Vosk cargado correctamente.");
    }

    /// <inheritdoc/>
    public async Task<string> TranscribeAsync(string audioUrl, string authUser, string authPassword)
    {
        var tempFile = Path.GetTempFileName();

        try
        {
            // 1. Descargar el audio con autenticación HTTP Basic
            await DownloadAudioAsync(audioUrl, authUser, authPassword, tempFile);

            // 2. Decodificar Ogg/Opus → PCM 16kHz mono y transcribir
            return TranscribeFromFile(tempFile);
        }
        finally
        {
            // 3. Limpiar archivo temporal siempre (éxito o excepción)
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo eliminar el archivo temporal: {TempFile}", tempFile);
                }
            }
        }
    }

    private async Task DownloadAudioAsync(
        string audioUrl, string authUser, string authPassword, string destinationPath)
    {
        using var client = _httpClientFactory.CreateClient();
        var credentials = Convert.ToBase64String(
            System.Text.Encoding.ASCII.GetBytes($"{authUser}:{authPassword}"));
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);

        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(audioUrl);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"No se pudo descargar el audio desde '{audioUrl}': {ex.Message}", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Error al descargar el audio desde '{audioUrl}': HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        await using var fileStream = File.Create(destinationPath);
        await response.Content.CopyToAsync(fileStream);
        _logger.LogDebug("Audio descargado a archivo temporal: {TempFile}", destinationPath);
    }

    private string TranscribeFromFile(string oggFilePath)
    {
        // Concentus 2.2.2: OpusDecoder está en Concentus.Structs, constructor directo
        // Concentus.Oggfile 1.0.7: namespace es Concentus.Oggfile (f minúscula)
        // Se usa nombre completo para evitar colisión con el namespace raíz Concentus
        using var fileIn = File.OpenRead(oggFilePath);
        var decoder = new OpusDecoder(16000, 1);
        var oggIn = new Concentus.Oggfile.OpusOggReadStream(decoder, fileIn);

        // Vosk 0.3.38: VoskRecognizer sigue siendo el nombre correcto del reconocedor
        using var recognizer = new VoskRecognizer(_model, 16000.0f);
        recognizer.SetMaxAlternatives(0);
        recognizer.SetWords(false);

        while (oggIn.HasNextPacket)
        {
            var pcmShorts = oggIn.DecodeNextPacket();
            if (pcmShorts == null || pcmShorts.Length == 0) continue;

            // Convertir short[] a byte[] (little-endian PCM 16-bit) para Vosk
            var pcmBytes = new byte[pcmShorts.Length * 2];
            Buffer.BlockCopy(pcmShorts, 0, pcmBytes, 0, pcmBytes.Length);

            recognizer.AcceptWaveform(pcmBytes, pcmBytes.Length);
        }

        var resultJson = recognizer.FinalResult();
        _logger.LogDebug("Resultado Vosk (JSON): {Result}", resultJson);

        // Extraer el campo "text" del JSON de Vosk: {"text": "..."}
        using var doc = JsonDocument.Parse(resultJson);
        if (doc.RootElement.TryGetProperty("text", out var textElement))
        {
            var text = textElement.GetString() ?? string.Empty;
            return text.Trim();
        }

        return string.Empty;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _model?.Dispose();
        _disposed = true;
    }
}
