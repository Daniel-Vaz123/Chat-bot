namespace Chatbot.Models;

/// <summary>
/// Resultado de una búsqueda en el índice vectorial
/// </summary>
public class SearchResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<VectorMatch> Matches { get; set; } = new();
}

/// <summary>
/// Representa un match individual en la búsqueda vectorial
/// </summary>
public class VectorMatch
{
    public double Distance { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    
    /// <summary>
    /// Calcula el porcentaje de similitud basado en la distancia coseno
    /// </summary>
    public double SimilarityPercentage => (1 - Distance / 2) * 100;
}

/// <summary>
/// Resultado de la carga de imágenes al índice
/// </summary>
public class LoadImagesResult
{
    public int TotalImages { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<ImageLoadStatus> ImageStatuses { get; set; } = new();
}

/// <summary>
/// Estado de carga de una imagen individual
/// </summary>
public class ImageLoadStatus
{
    public string Description { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Resultado de un test de embeddings
/// </summary>
public class EmbeddingTestResult
{
    public string Text1 { get; set; } = string.Empty;
    public string Text2 { get; set; } = string.Empty;
    public double Similarity { get; set; }
    public double Distance { get; set; }
    
    public string SimilarityLevel
    {
        get
        {
            if (Distance < 0.3) return "Muy similar";
            if (Distance < 0.6) return "Similar";
            if (Distance < 1.0) return "Algo similar";
            return "Poco similar";
        }
    }
}
