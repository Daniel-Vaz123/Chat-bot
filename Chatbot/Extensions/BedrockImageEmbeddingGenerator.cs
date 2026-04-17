using System.Text;
using System.Text.Json;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Microsoft.Extensions.AI;

namespace Chatbot.Extensions;

/// <summary>
/// Implementación de IEmbeddingGenerator para Amazon Titan Multimodal Embeddings
/// Compatible con el patrón de Semantic Kernel
/// </summary>
public sealed class BedrockImageEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly IAmazonBedrockRuntime _bedrockRuntime;
    private readonly string _modelId;
    private readonly int _outputLength;

    public BedrockImageEmbeddingGenerator(
        IAmazonBedrockRuntime bedrockRuntime,
        string modelId,
        int outputLength = 1024)
    {
        _bedrockRuntime = bedrockRuntime ?? throw new ArgumentNullException(nameof(bedrockRuntime));
        _modelId = modelId ?? throw new ArgumentNullException(nameof(modelId));
        _outputLength = outputLength;
    }

    public EmbeddingGeneratorMetadata Metadata => new(
        providerName: "Amazon Bedrock",
        providerUri: new Uri("https://aws.amazon.com/bedrock/"),
        _modelId);

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var embeddings = new List<Embedding<float>>();

        foreach (var value in values)
        {
            var embedding = await GenerateSingleEmbeddingAsync(value, cancellationToken);
            embeddings.Add(embedding);
        }

        return new GeneratedEmbeddings<Embedding<float>>(embeddings);
    }

    public void Dispose()
    {
        // No hay recursos que liberar
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
    }

    private async Task<Embedding<float>> GenerateSingleEmbeddingAsync(
        string input,
        CancellationToken cancellationToken)
    {
        // Determinar si el input es una imagen (Base64) o texto
        var requestBody = IsBase64Image(input)
            ? CreateImageRequest(input)
            : CreateTextRequest(input);

        var jsonRequest = JsonSerializer.Serialize(requestBody);
        var requestBodyStream = new MemoryStream(Encoding.UTF8.GetBytes(jsonRequest));

        var request = new InvokeModelRequest
        {
            ModelId = _modelId,
            Body = requestBodyStream,
            ContentType = "application/json",
            Accept = "application/json"
        };

        var response = await _bedrockRuntime.InvokeModelAsync(request, cancellationToken);

        using var reader = new StreamReader(response.Body);
        var responseJson = await reader.ReadToEndAsync();
        
        // Configurar opciones de deserialización con camelCase
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        
        var responseData = JsonSerializer.Deserialize<TitanMultimodalResponse>(responseJson, options);

        if (responseData?.Embedding == null)
        {
            throw new InvalidOperationException("No se pudo obtener el embedding del modelo");
        }

        return new Embedding<float>(responseData.Embedding);
    }

    private object CreateImageRequest(string base64Image)
    {
        return new
        {
            inputImage = base64Image,
            embeddingConfig = new
            {
                outputEmbeddingLength = _outputLength
            }
        };
    }

    private object CreateTextRequest(string text)
    {
        return new
        {
            inputText = text,
            embeddingConfig = new
            {
                outputEmbeddingLength = _outputLength
            }
        };
    }
    
    /// <summary>
    /// Crea request con AMBOS imagen y texto (recomendado para multimodal)
    /// </summary>
    private object CreateMultimodalRequest(string base64Image, string text)
    {
        return new
        {
            inputImage = base64Image,
            inputText = text,
            embeddingConfig = new
            {
                outputEmbeddingLength = _outputLength
            }
        };
    }

    private static bool IsBase64Image(string input)
    {
        // Heurística: si es muy largo y no tiene espacios, probablemente es Base64
        // Las imágenes en Base64 típicamente tienen >1000 caracteres
        return input.Length > 500 && !input.Contains(' ') && !input.Contains('\n');
    }

    private class TitanMultimodalResponse
    {
        public float[]? Embedding { get; set; }
        public int? InputTextTokenCount { get; set; }
    }
}
