using Amazon.S3Vectors;
using Amazon.S3Vectors.Model;
using Microsoft.Extensions.Logging;

namespace Chatbot.Services;

/// <summary>
/// Gestiona la creación automática de índices en S3 Vectors bajo demanda (Lazy)
/// Compatible con AWS SDK v4
/// </summary>
public class S3VectorsIndexManager
{
    private readonly IAmazonS3Vectors _s3Vectors;
    private readonly ILogger<S3VectorsIndexManager> _logger;
    private readonly HashSet<string> _verifiedIndices = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public S3VectorsIndexManager(
        IAmazonS3Vectors s3Vectors,
        ILogger<S3VectorsIndexManager> logger)
    {
        _s3Vectors = s3Vectors;
        _logger = logger;
    }

    /// <summary>
    /// Asegura que el índice existe, creándolo automáticamente si es necesario
    /// </summary>
    /// <param name="bucketName">Nombre del bucket de vectores</param>
    /// <param name="indexName">Nombre del índice</param>
    /// <param name="dimensions">Número de dimensiones del vector (1-4096)</param>
    /// <param name="distanceMetric">Métrica de distancia: "cosine" o "euclidean"</param>
    public async Task EnsureIndexExistsAsync(
        string bucketName,
        string indexName,
        int dimensions = 1024,
        string distanceMetric = "cosine")
    {
        var key = $"{bucketName}/{indexName}";

        // Verificación rápida en caché (sin lock)
        if (_verifiedIndices.Contains(key))
        {
            return;
        }

        // Lock para evitar race conditions
        await _lock.WaitAsync();
        try
        {
            // Double-check después del lock
            if (_verifiedIndices.Contains(key))
            {
                return;
            }

            _logger.LogDebug("Verificando índice '{IndexName}' en bucket '{BucketName}'...", 
                indexName, bucketName);

            // Verificar si el índice existe
            bool exists = await IndexExistsAsync(bucketName, indexName);

            if (!exists)
            {
                _logger.LogWarning("⚠️  Índice '{IndexName}' no encontrado, creando automáticamente...", indexName);
                
                await CreateIndexAsync(bucketName, indexName, dimensions, distanceMetric);
                
                _logger.LogInformation("✓ Índice '{IndexName}' creado exitosamente ({Dimensions} dimensiones, métrica: {Metric})",
                    indexName, dimensions, distanceMetric);
            }
            else
            {
                _logger.LogDebug("✓ Índice '{IndexName}' ya existe", indexName);
            }

            // Marcar como verificado en caché
            _verifiedIndices.Add(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al verificar/crear índice '{IndexName}' en bucket '{BucketName}'",
                indexName, bucketName);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Verifica si un índice existe usando ListIndexes
    /// </summary>
    private async Task<bool> IndexExistsAsync(string bucketName, string indexName)
    {
        try
        {
            var request = new ListIndexesRequest
            {
                VectorBucketName = bucketName,
                Prefix = indexName,
                MaxResults = 10
            };

            var response = await _s3Vectors.ListIndexesAsync(request);
            
            // Verificar si existe un índice con el nombre exacto
            return response.Indexes?.Any(idx => idx.IndexName == indexName) ?? false;
        }
        catch (AmazonS3VectorsException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // El bucket no existe
            _logger.LogWarning("Bucket '{BucketName}' no encontrado", bucketName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al listar índices en bucket '{BucketName}'", bucketName);
            throw;
        }
    }

    /// <summary>
    /// Crea un nuevo índice vectorial usando la API de AWS SDK v4
    /// </summary>
    private async Task CreateIndexAsync(
        string bucketName,
        string indexName,
        int dimensions,
        string distanceMetric)
    {
        try
        {
            var request = new CreateIndexRequest
            {
                VectorBucketName = bucketName,
                IndexName = indexName,
                DataType = "float32",
                Dimension = dimensions,
                DistanceMetric = distanceMetric.ToLower() // "cosine" o "euclidean"
            };

            var response = await _s3Vectors.CreateIndexAsync(request);
            
            _logger.LogDebug("Índice creado: {IndexName}", indexName);
        }
        catch (AmazonS3VectorsException ex) when (ex.Message.Contains("already exists"))
        {
            // El índice ya existe (race condition), no es un error
            _logger.LogDebug("El índice '{IndexName}' ya existe (creado por otro proceso)", indexName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear índice '{IndexName}' en bucket '{BucketName}'",
                indexName, bucketName);
            throw;
        }
    }

    /// <summary>
    /// Limpia la caché de índices verificados (útil para testing)
    /// </summary>
    public void ClearCache()
    {
        _verifiedIndices.Clear();
        _logger.LogDebug("Caché de índices limpiada");
    }
}
