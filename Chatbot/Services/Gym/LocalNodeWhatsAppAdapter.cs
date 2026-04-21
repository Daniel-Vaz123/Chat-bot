using System.Text.Json;
using Chatbot.Services;
using Chatbot.Services.Gym;

namespace Chatbot.Services.Gym;

public class LocalNodeWhatsAppAdapter : IChannelAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LocalNodeWhatsAppAdapter> _logger;
    private readonly string _bridgeUrl = "http://localhost:3000/send";

    public LocalNodeWhatsAppAdapter(HttpClient httpClient, ILogger<LocalNodeWhatsAppAdapter> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task SendMessageAsync(string userId, string messageContent)
    {
        try
        {
            var payload = new
            {
                userId = userId,
                message = messageContent
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_bridgeUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Error enviando mensaje al puente de Node.js: HTTP {StatusCode} - {Error}", response.StatusCode, errorBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al conectar con el puente de Node.js http://localhost:3000/send. Asegúrate de ejecutar 'node index.js'.");
        }
    }

}
