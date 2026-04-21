using System.Net.Http.Headers;
using System.Text.Json;
using Chatbot.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chatbot.Services;

public interface IDeepSeekAiService
{
    Task<string> GetRAGAnswerAsync(string context, string query);
}

public class DeepSeekAiService : IDeepSeekAiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<DeepSeekAiService> _logger;

    public DeepSeekAiService(HttpClient httpClient, IOptions<AppSettings> settings, ILogger<DeepSeekAiService> logger)
    {
        _httpClient = httpClient;
        _apiKey = settings.Value.DeepSeek.ApiKey;
        _logger = logger;
    }

    public async Task<string> GetRAGAnswerAsync(string context, string query)
    {
        // Validación de Key
        if (string.IsNullOrEmpty(_apiKey) || _apiKey.Contains("REEMPLAZAR"))
        {
            _logger.LogWarning("DeepSeek API Key no configurada.");
            return "El servidor no tiene configurada la clave de IA. Avísale a mi creador.";
        }

        // Estructura oficial de la API de DeepSeek (compatible con formato OpenAI)
        var requestBody = new
        {
            model = "deepseek-chat", // Modelo general
            messages = new[]
            {
                new { 
                    role = "system", 
                    content = $"Eres el asistente virtual amable de este gimnasio. Responde a las preguntas del usuario basándote ESTRICTAMENTE en la siguiente información del gimnasio recuperada de la base de datos:\n\n{context}\n\nREGLAS: Sé conciso y amigable. Si la información no está en el texto proporcionado, di amablemente que no tienes el dato disponible y no inventes." 
                },
                new { 
                    role = "user", 
                    content = query 
                }
            },
            temperature = 0.3 // Baja temperatura para que sea conciso y se apegue a los datos
        };

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.deepseek.com/chat/completions");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        requestMessage.Content = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.SendAsync(requestMessage);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("DeepSeek API error: {Status} - {Content}", response.StatusCode, await response.Content.ReadAsStringAsync());
                return "Tuve un pequeño problema de conexión mental. Intenta de nuevo en unos momentos.";
            }

            var jsonStr = await response.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(jsonStr);
            var answer = jsonDoc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

            return answer ?? "No pude formular una respuesta correcta.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción al comunicarse con DeepSeek API.");
            return "Se agotó el tiempo de espera. Probablemente mi cerebro está saturado.";
        }
    }
}
