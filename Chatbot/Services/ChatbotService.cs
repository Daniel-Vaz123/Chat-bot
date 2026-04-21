using System.Text.Json;
using Amazon.S3Vectors;
using Amazon.S3Vectors.Model;
using Amazon.Runtime.Documents;
using Chatbot.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.AI;


namespace Chatbot.Services;

public class ChatbotService
{
    private readonly IAmazonS3Vectors _s3Vectors;
    private readonly Kernel _kernel;
    private readonly S3VectorsIndexManager _indexManager;
    private readonly MultimodalEmbeddingHelper _multimodalHelper;
    private readonly IDeepSeekAiService _deepSeekService;
    private readonly string _vectorBucketName;
    private readonly string _indexName;
    private readonly string _imageIndexName;
    private readonly int _textEmbeddingDimensions;
    private readonly int _imageEmbeddingDimensions;
    private readonly double _maxQueryDistance;

    public ChatbotService(
        IAmazonS3Vectors s3Vectors,
        Kernel kernel,
        S3VectorsIndexManager indexManager,
        MultimodalEmbeddingHelper multimodalHelper,
        IDeepSeekAiService deepSeekService,
        IOptions<AppSettings> settings)
    {
        _s3Vectors = s3Vectors;
        _kernel = kernel;
        _indexManager = indexManager;
        _multimodalHelper = multimodalHelper;
        _deepSeekService = deepSeekService;
        _vectorBucketName = settings.Value.S3Vectors.BucketName;
        _indexName = settings.Value.S3Vectors.IndexName;
        _imageIndexName = settings.Value.S3Vectors.ImageIndexName;
        _textEmbeddingDimensions = settings.Value.S3Vectors.TextEmbeddingDimensions;
        _imageEmbeddingDimensions = settings.Value.S3Vectors.ImageEmbeddingDimensions;
        _maxQueryDistance = settings.Value.S3Vectors.MaxQueryDistance;
    }

    #region Métodos de Carga (Ingestión)

    public async Task LoadQuestionsToVectorStoreAsync(string jsonFilePath)
    {
        var jsonContent = await File.ReadAllTextAsync(jsonFilePath);
        var chatbotData = JsonSerializer.Deserialize<ChatbotData>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (chatbotData?.QaPairs == null) return;

        // Asegurar que el índice existe antes de insertar
        await _indexManager.EnsureIndexExistsAsync(
            _vectorBucketName, 
            _indexName, 
            _textEmbeddingDimensions);

        var embeddingGenerator = _kernel.Services.GetRequiredKeyedService<IEmbeddingGenerator<string, Embedding<float>>>("text-embeddings");
        var vectors = new List<PutInputVector>();

        foreach (var qaPair in chatbotData.QaPairs)
        {
            var result = await embeddingGenerator.GenerateAsync([qaPair.Question]);
            var vector = result[0].Vector;

            var metadata = new Dictionary<string, Document>
            {
                { "question", new Document(qaPair.Question) },
                { "answer", new Document(qaPair.Answer) },
                { "type", new Document(qaPair.Type) },
                { "category", new Document(qaPair.Category) }
            };

            vectors.Add(new PutInputVector
            {
                Key = $"qa-{Guid.NewGuid()}",
                Data = new VectorData { Float32 = vector.ToArray().ToList() },
                Metadata = new Document(metadata)
            });
        }

        await _s3Vectors.PutVectorsAsync(new PutVectorsRequest { VectorBucketName = _vectorBucketName, IndexName = _indexName, Vectors = vectors });
    }

    public async Task<LoadImagesResult> LoadImagesToVectorStoreAsync(string jsonFilePath)
    {
        var result = new LoadImagesResult();
        
        var jsonContent = await File.ReadAllTextAsync(jsonFilePath);
        var imagesData = JsonSerializer.Deserialize<List<ImageEntry>>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (imagesData == null) return result;

        result.TotalImages = imagesData.Count;

        // Asegurar que el índice de imágenes existe antes de insertar
        await _indexManager.EnsureIndexExistsAsync(
            _vectorBucketName, 
            _imageIndexName, 
            _imageEmbeddingDimensions);

        var vectors = new List<PutInputVector>();
        using var httpClient = new HttpClient();

        foreach (var item in imagesData)
        {
            var status = new ImageLoadStatus { Description = item.Description };
            
            try
            {
                // Convertir URLs de Google Drive al formato descargable
                var downloadUrl = ConvertGoogleDriveUrl(item.ImageUrl);
                
                byte[] imageBytes = downloadUrl.StartsWith("http")
                    ? await httpClient.GetByteArrayAsync(downloadUrl)
                    : await File.ReadAllBytesAsync(downloadUrl);

                string base64Image = Convert.ToBase64String(imageBytes);
                
                // IMPORTANTE: Usar el helper multimodal que envía imagen + texto juntos
                // Esto mejora significativamente la búsqueda texto→imagen según AWS
                var embedding = await _multimodalHelper.GenerateMultimodalEmbeddingAsync(base64Image, item.Description);

                var metadata = new Dictionary<string, Document>
                {
                    { "description", new Document(item.Description) },
                    { "category", new Document(item.Category) },
                    { "imageUrl", new Document(item.ImageUrl) },
                    { "location", new Document(item.Location) },
                };

                vectors.Add(new PutInputVector
                {
                    Key = $"img-{Guid.NewGuid()}",
                    Data = new VectorData { Float32 = embedding.ToList() },
                    Metadata = new Document(metadata)
                });
                
                status.Success = true;
                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                status.Success = false;
                status.ErrorMessage = ex.Message;
                result.FailureCount++;
            }
            
            result.ImageStatuses.Add(status);
        }

        await _s3Vectors.PutVectorsAsync(new PutVectorsRequest { VectorBucketName = _vectorBucketName, IndexName = _imageIndexName, Vectors = vectors });
        
        return result;
    }

    #endregion

    #region Métodos de Consulta (Búsqueda)

    public async Task<string> AskQuestionAsync(string userQuestion)
    {
        // Asegurar que el índice existe antes de consultar
        await _indexManager.EnsureIndexExistsAsync(
            _vectorBucketName, 
            _indexName, 
            _textEmbeddingDimensions);

        var embeddingGenerator = _kernel.Services.GetRequiredKeyedService<IEmbeddingGenerator<string, Embedding<float>>>("text-embeddings");
        var embeddings = await embeddingGenerator.GenerateAsync([userQuestion]);
        var questionEmbedding = embeddings[0].Vector;

        var response = await QueryS3VectorsAsync(questionEmbedding);
        
        // Criterio de similitud: si la distancia es muy grande, no usamos la API para ahorrar tokens
        if (response.Vectors.Count == 0 || response.Vectors[0].Distance > _maxQueryDistance)
            return "Lo siento, no encuentro esa información en nuestra base de datos. ¿Te puedo ayudar con precios u horarios?";

        // Armamos el contexto a mandarle a DeepSeek
        var metadata = response.Vectors[0].Metadata.AsDictionary();
        string context = $"Pregunta encontrada en base: {metadata["question"].AsString()}\n" +
                         $"Información oficial: {metadata["answer"].AsString()}";

        // Pasamos por la IA mágica de DeepSeek para darle formato humano
        return await _deepSeekService.GetRAGAnswerAsync(context, userQuestion);
    }

    // Búsqueda: El usuario envía una imagen y recuperamos su descripción/URL
    public async Task<string> SearchByImageAsync(Stream imageStream)
    {
        // Asegurar que el índice de imágenes existe antes de consultar
        await _indexManager.EnsureIndexExistsAsync(
            _vectorBucketName, 
            _imageIndexName, 
            _imageEmbeddingDimensions);

        // Obtener el generador de embeddings de imágenes (patrón Semantic Kernel)
        var imageEmbeddingGenerator = _kernel.Services.GetRequiredKeyedService<IEmbeddingGenerator<string, Embedding<float>>>("image-embeddings");
        
        using var ms = new MemoryStream();
        await imageStream.CopyToAsync(ms);
        
        string base64Image = Convert.ToBase64String(ms.ToArray());
        var result = await imageEmbeddingGenerator.GenerateAsync([base64Image]);
        var embedding = result[0].Vector;

        var response = await QueryS3ImageVectorsAsync(embedding);
        if (response.Vectors.Count == 0) return "Imagen no reconocida.";

        var meta = response.Vectors[0].Metadata.AsDictionary();
        return $"Encontrado: {meta["description"].AsString()} | URL: {meta["imageUrl"].AsString()}";
    }

    // NUEVO MÉTODO: Búsqueda de imagen mediante URL (Multimodal)
    public async Task<SearchResult> SearchImageByUrlAsync(string imageUrl)
    {
        var result = new SearchResult();
        
        // Asegurar que el índice de imágenes existe antes de consultar
        await _indexManager.EnsureIndexExistsAsync(
            _vectorBucketName, 
            _imageIndexName, 
            _imageEmbeddingDimensions);

        try
        {
            using var httpClient = new HttpClient();
            
            // Convertir URLs de Google Drive al formato descargable
            var downloadUrl = ConvertGoogleDriveUrl(imageUrl);
            
            byte[] imageBytes = downloadUrl.StartsWith("http")
                ? await httpClient.GetByteArrayAsync(downloadUrl)
                : await File.ReadAllBytesAsync(downloadUrl);
            
            string base64Image = Convert.ToBase64String(imageBytes);
            
            // Generar embedding solo de la imagen (sin texto)
            var imageEmbeddingGenerator = _kernel.Services.GetRequiredKeyedService<IEmbeddingGenerator<string, Embedding<float>>>("image-embeddings");
            var embeddingResult = await imageEmbeddingGenerator.GenerateAsync([base64Image]);
            var embedding = embeddingResult[0].Vector;
            
            var response = await QueryS3ImageVectorsAsync(embedding);
            
            if (response.Vectors.Count == 0)
            {
                result.Success = false;
                result.ErrorMessage = "No se encontraron imágenes similares en el índice.";
                return result;
            }
            
            // Convertir los resultados a VectorMatch
            foreach (var vector in response.Vectors)
            {
                var meta = vector.Metadata.AsDictionary();
                result.Matches.Add(new VectorMatch
                {
                    Distance = (double)vector.Distance,
                    Description = meta["description"].AsString(),
                    Category = meta["category"].AsString(),
                    ImageUrl = meta["imageUrl"].AsString(),
                    Location = meta["location"].AsString()
                });
            }
            
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }
        
        return result;
    }

    // NUEVO MÉTODO: Búsqueda de imagen mediante una descripción textual (Multimodal)
    public async Task<SearchResult> SearchImageByTextAsync(string searchText)
    {
        var result = new SearchResult();
        
        // Asegurar que el índice de imágenes existe antes de consultar
        await _indexManager.EnsureIndexExistsAsync(
            _vectorBucketName, 
            _imageIndexName, 
            _imageEmbeddingDimensions);

        // Obtener el generador de embeddings de imágenes (patrón Semantic Kernel)
        // El mismo generador maneja texto e imágenes en el espacio multimodal
        var imageEmbeddingGenerator = _kernel.Services.GetRequiredKeyedService<IEmbeddingGenerator<string, Embedding<float>>>("image-embeddings");
        
        var embeddingResult = await imageEmbeddingGenerator.GenerateAsync([searchText]);
        var embedding = embeddingResult[0].Vector;
        
        var response = await QueryS3ImageVectorsAsync(embedding);
        
        if (response.Vectors.Count == 0)
        {
            result.Success = false;
            result.ErrorMessage = "No se encontraron imágenes en el índice. ¿Has cargado imágenes primero?";
            return result;
        }
        
        // Convertir los resultados a VectorMatch
        foreach (var vector in response.Vectors)
        {
            var meta = vector.Metadata.AsDictionary();
            result.Matches.Add(new VectorMatch
            {
                Distance = (double)vector.Distance,
                Description = meta["description"].AsString(),
                Category = meta["category"].AsString(),
                ImageUrl = meta["imageUrl"].AsString(),
                Location = meta["location"].AsString()
            });
        }
        
        result.Success = true;
        return result;
    }

    #endregion

    #region Helpers

    private static string ConvertGoogleDriveUrl(string url)
    {
        // Convertir URLs de Google Drive al formato descargable
        if (url.Contains("drive.google.com/file/d/"))
        {
            var fileId = url.Split("/d/")[1].Split("/")[0];
            return $"https://drive.google.com/uc?export=download&id={fileId}";
        }
        return url;
    }

    private async Task<QueryVectorsResponse> QueryS3VectorsAsync(ReadOnlyMemory<float> vector)
    {
        return await _s3Vectors.QueryVectorsAsync(new QueryVectorsRequest
        {
            VectorBucketName = _vectorBucketName,
            IndexName = _indexName,
            TopK = 1,
            QueryVector = new VectorData { Float32 = vector.ToArray().ToList() },
            ReturnMetadata = true,
            ReturnDistance = true
        });
    }

    private async Task<QueryVectorsResponse> QueryS3ImageVectorsAsync(ReadOnlyMemory<float> vector)
    {
        return await _s3Vectors.QueryVectorsAsync(new QueryVectorsRequest
        {
            VectorBucketName = _vectorBucketName,
            IndexName = _imageIndexName,
            TopK = 5, // Retornar top 5 para ver más opciones
            QueryVector = new VectorData { Float32 = vector.ToArray().ToList() },
            ReturnMetadata = true,
            ReturnDistance = true
        });
    }


    #endregion
}

public record ImageEntry(string Description, string ImageUrl,string Category,string Location);

