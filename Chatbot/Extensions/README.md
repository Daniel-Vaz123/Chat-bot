# Extensiones de Semantic Kernel para Amazon Bedrock

Este directorio contiene extensiones personalizadas para Semantic Kernel que agregan soporte completo para embeddings multimodales de Amazon Titan.

## Estructura

```
Extensions/
├── BedrockImageEmbeddingGenerator.cs      # Implementación del generador
├── BedrockImageEmbeddingExtensions.cs     # Métodos de extensión para DI
└── README.md                              # Este archivo
```

## BedrockImageEmbeddingGenerator

Implementa `IEmbeddingGenerator<string, Embedding<float>>` de `Microsoft.Extensions.AI` para generar embeddings usando el modelo Amazon Titan Multimodal.

### Características Principales

1. **Detección Automática de Tipo de Input**
   - Detecta si el input es una imagen (Base64) o texto
   - Construye el payload JSON apropiado para cada caso

2. **Soporte Multimodal**
   - Imágenes → Embeddings
   - Texto → Embeddings (en el mismo espacio vectorial)
   - Permite búsqueda cruzada texto-imagen

3. **Configuración Flexible**
   - Soporta dimensiones: 256, 384, 1024
   - Configurable en tiempo de registro

### Implementación

```csharp
public sealed class BedrockImageEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Genera embeddings para cada valor
        // Detecta automáticamente si es imagen o texto
    }
}
```

### Detección de Tipo de Input

```csharp
private static bool IsBase64Image(string input)
{
    // Heurística: si es muy largo y no tiene espacios, probablemente es Base64
    return input.Length > 500 && !input.Contains(' ') && !input.Contains('\n');
}
```

**Criterios:**
- Longitud > 500 caracteres
- Sin espacios ni saltos de línea
- Típicamente las imágenes en Base64 tienen >1000 caracteres

## BedrockImageEmbeddingExtensions

Proporciona métodos de extensión para registrar el generador en el contenedor de DI, siguiendo el patrón de Semantic Kernel.

### Métodos Disponibles

#### 1. Para IServiceCollection

```csharp
public static IServiceCollection AddBedrockImageEmbeddingGenerator(
    this IServiceCollection services,
    string modelId,
    string? serviceId = null,
    int outputLength = 1024)
```

**Uso:**
```csharp
services.AddBedrockImageEmbeddingGenerator(
    modelId: "amazon.titan-embed-image-v1",
    serviceId: "image-embeddings",
    outputLength: 1024
);
```

#### 2. Para IKernelBuilder

```csharp
public static IKernelBuilder AddBedrockImageEmbeddingGenerator(
    this IKernelBuilder builder,
    string modelId,
    string? serviceId = null,
    int outputLength = 1024)
```

**Uso:**
```csharp
kernelBuilder.AddBedrockImageEmbeddingGenerator(
    modelId: "amazon.titan-embed-image-v1",
    serviceId: "image-embeddings",
    outputLength: 1024
);
```

### Validaciones

La extensión valida:
- `services` y `builder` no sean null
- `modelId` no sea null o vacío
- `outputLength` sea 256, 384, o 1024

## Patrón de Uso Completo

### 1. Registro en Program.cs

```csharp
var kernelBuilder = Kernel.CreateBuilder();

// Registrar cliente de Bedrock
kernelBuilder.Services.AddSingleton<IAmazonBedrockRuntime>(bedrockClient);

// Registrar generador de texto (oficial)
kernelBuilder.Services.AddBedrockEmbeddingGenerator(
    modelId: "amazon.titan-embed-text-v2:0",
    serviceId: "text-embeddings"
);

// Registrar generador de imagen (nuestra extensión)
kernelBuilder.AddBedrockImageEmbeddingGenerator(
    modelId: "amazon.titan-embed-image-v1",
    serviceId: "image-embeddings",
    outputLength: 1024
);

var kernel = kernelBuilder.Build();
```

### 2. Uso en Servicios

```csharp
public class ChatbotService
{
    private readonly Kernel _kernel;

    public async Task<string> SearchImageByTextAsync(string searchText)
    {
        // Obtener el generador (mismo patrón que texto)
        var generator = _kernel.Services
            .GetRequiredKeyedService<IEmbeddingGenerator<string, Embedding<float>>>("image-embeddings");
        
        // Generar embedding
        var result = await generator.GenerateAsync([searchText]);
        var embedding = result[0].Vector;
        
        // Usar el embedding para búsqueda
        // ...
    }
}
```

## Comparación con Conector Oficial

### Conector Oficial de Bedrock (Texto)

```csharp
// Usa BedrockTextEmbeddingGenerationClient internamente
kernelBuilder.Services.AddBedrockEmbeddingGenerator(
    modelId: "amazon.titan-embed-text-v2:0",
    serviceId: "text-embeddings"
);
```

**Payload generado:**
```json
{
  "inputText": "texto a procesar"
}
```

### Nuestra Extensión (Multimodal)

```csharp
// Usa BedrockImageEmbeddingGenerator
kernelBuilder.AddBedrockImageEmbeddingGenerator(
    modelId: "amazon.titan-embed-image-v1",
    serviceId: "image-embeddings"
);
```

**Payload generado para imagen:**
```json
{
  "inputImage": "base64_encoded_image",
  "embeddingConfig": {
    "outputEmbeddingLength": 1024
  }
}
```

**Payload generado para texto:**
```json
{
  "inputText": "texto a procesar",
  "embeddingConfig": {
    "outputEmbeddingLength": 1024
  }
}
```

## Testing

### Unit Tests

```csharp
[Fact]
public async Task GenerateAsync_WithBase64Image_ReturnsEmbedding()
{
    // Arrange
    var mockBedrockRuntime = new Mock<IAmazonBedrockRuntime>();
    var generator = new BedrockImageEmbeddingGenerator(
        mockBedrockRuntime.Object,
        "amazon.titan-embed-image-v1",
        1024
    );
    
    var base64Image = Convert.ToBase64String(new byte[1000]);
    
    // Act
    var result = await generator.GenerateAsync([base64Image]);
    
    // Assert
    Assert.NotNull(result);
    Assert.Single(result);
}
```

### Integration Tests

```csharp
[Fact]
public async Task SearchImageByText_ReturnsRelevantImage()
{
    // Arrange
    var kernel = CreateKernelWithImageEmbeddings();
    var service = new ChatbotService(s3Vectors, kernel, settings);
    
    // Act
    var result = await service.SearchImageByTextAsync("pantalon gabardina");
    
    // Assert
    Assert.Contains("pantalon", result.ToLower());
}
```

## Mejoras Futuras

1. **Caché de Embeddings**: Evitar regenerar embeddings para las mismas imágenes
2. **Batch Processing**: Procesar múltiples imágenes en paralelo
3. **Retry Logic**: Reintentar en caso de errores transitorios
4. **Métricas**: Agregar telemetría y logging
5. **Validación de Imágenes**: Verificar formato y tamaño antes de enviar

## Contribuir

Si encuentras un bug o tienes una mejora:

1. Asegúrate de que sigue el patrón de Semantic Kernel
2. Agrega tests unitarios
3. Actualiza esta documentación
4. Mantén la compatibilidad con `IEmbeddingGenerator<string, Embedding<float>>`

## Referencias

- [Semantic Kernel Documentation](https://learn.microsoft.com/en-us/semantic-kernel/)
- [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI)
- [Amazon Bedrock Runtime API](https://docs.aws.amazon.com/bedrock/latest/APIReference/API_runtime_InvokeModel.html)
- [Amazon Titan Multimodal Embeddings](https://docs.aws.amazon.com/bedrock/latest/userguide/titan-multiemb-models.html)
