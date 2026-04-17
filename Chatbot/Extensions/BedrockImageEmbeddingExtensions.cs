using Amazon.BedrockRuntime;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace Chatbot.Extensions;

/// <summary>
/// Métodos de extensión para registrar el generador de embeddings de imágenes de Bedrock
/// Sigue el patrón de Semantic Kernel para consistencia
/// </summary>
public static class BedrockImageEmbeddingExtensions
{
    /// <summary>
    /// Agrega el generador de embeddings de imágenes de Amazon Bedrock al contenedor de servicios
    /// </summary>
    /// <param name="services">Colección de servicios</param>
    /// <param name="modelId">ID del modelo (ej: amazon.titan-embed-image-v1)</param>
    /// <param name="serviceId">ID del servicio para recuperarlo con GetRequiredKeyedService</param>
    /// <param name="outputLength">Longitud del vector de embedding (256, 384, o 1024)</param>
    /// <returns>La colección de servicios para encadenamiento</returns>
    public static IServiceCollection AddBedrockImageEmbeddingGenerator(
        this IServiceCollection services,
        string modelId,
        string? serviceId = null,
        int outputLength = 1024)
    {
        Verify.NotNull(services);
        Verify.NotNullOrWhiteSpace(modelId);

        if (outputLength != 256 && outputLength != 384 && outputLength != 1024)
        {
            throw new ArgumentException("outputLength debe ser 256, 384, o 1024", nameof(outputLength));
        }

        services.AddKeyedSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
            serviceId,
            (sp, key) =>
            {
                var bedrockRuntime = sp.GetRequiredService<IAmazonBedrockRuntime>();
                return new BedrockImageEmbeddingGenerator(bedrockRuntime, modelId, outputLength);
            });

        return services;
    }

    /// <summary>
    /// Agrega el generador de embeddings de imágenes de Amazon Bedrock al Kernel Builder
    /// </summary>
    /// <param name="builder">Kernel builder</param>
    /// <param name="modelId">ID del modelo (ej: amazon.titan-embed-image-v1)</param>
    /// <param name="serviceId">ID del servicio para recuperarlo con GetRequiredKeyedService</param>
    /// <param name="outputLength">Longitud del vector de embedding (256, 384, o 1024)</param>
    /// <returns>El kernel builder para encadenamiento</returns>
    public static IKernelBuilder AddBedrockImageEmbeddingGenerator(
        this IKernelBuilder builder,
        string modelId,
        string? serviceId = null,
        int outputLength = 1024)
    {
        Verify.NotNull(builder);
        builder.Services.AddBedrockImageEmbeddingGenerator(modelId, serviceId, outputLength);
        return builder;
    }
}

/// <summary>
/// Clase de utilidad para validaciones (similar a la que usa Semantic Kernel internamente)
/// </summary>
internal static class Verify
{
    public static void NotNull(object? obj, string? paramName = null)
    {
        if (obj is null)
        {
            throw new ArgumentNullException(paramName ?? "value");
        }
    }

    public static void NotNullOrWhiteSpace(string? value, string? paramName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("El valor no puede ser nulo o vacío", paramName ?? "value");
        }
    }
}
