using System.Text;
using System.Text.Json;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;

namespace Chatbot.Services;

/// <summary>
/// Helper para generar embeddings multimodales (imagen + texto juntos)
/// Esto mejora significativamente la búsqueda cruzada texto→imagen
/// </summary>
public class MultimodalEmbeddingHelper
{
    private readonly IAmazonBedrockRuntime _bedrockRuntime;
    private readonly string _modelId;
    private readonly int _outputLength;

    public MultimodalEmbeddingHelper(IAmazonBedrockRuntime bedrockRuntime, string modelId, int outputLength = 1024)
    {
        _bedrockRuntime = bedrockRuntime;
        _modelId = modelId;
        _outputLength = outputLength;
    }

    /// <summary>
    /// Genera embedding combinando imagen Y texto
    /// Recomendado por AWS para mejor búsqueda multimodal
    /// </summary>
    public async Task<float[]> GenerateMultimodalEmbeddingAsync(string base64Image, string text)
    {
        var requestBody = new
        {
            inputImage = base64Image,
            inputText = text,
            embeddingConfig = new
            {
                outputEmbeddingLength = _outputLength
            }
        };

        var jsonRequest = JsonSerializer.Serialize(requestBody);
        var requestBodyStream = new MemoryStream(Encoding.UTF8.GetBytes(jsonRequest));

        var request = new InvokeModelRequest
        {
            ModelId = _modelId,
            Body = requestBodyStream,
            ContentType = "application/json",
            Accept = "application/json"
        };

        var response = await _bedrockRuntime.InvokeModelAsync(request);

        using var reader = new StreamReader(response.Body);
        var responseJson = await reader.ReadToEndAsync();
        
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        
        var responseData = JsonSerializer.Deserialize<TitanResponse>(responseJson, options);

        return responseData?.Embedding ?? throw new InvalidOperationException("No se pudo obtener el embedding");
    }

    private class TitanResponse
    {
        public float[]? Embedding { get; set; }
        public int? InputTextTokenCount { get; set; }
    }
}
