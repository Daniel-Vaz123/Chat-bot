using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Chatbot.Models;

namespace Chatbot.Services;

/// <summary>
/// Helper para debuggear embeddings y verificar similitud
/// </summary>
public class EmbeddingDebugHelper
{
    private readonly Kernel _kernel;

    public EmbeddingDebugHelper(Kernel kernel)
    {
        _kernel = kernel;
    }

    /// <summary>
    /// Calcula la similitud coseno entre dos textos
    /// </summary>
    public async Task<double> CalculateTextSimilarityAsync(string text1, string text2)
    {
        var generator = _kernel.Services.GetRequiredKeyedService<IEmbeddingGenerator<string, Embedding<float>>>("image-embeddings");
        
        var result1 = await generator.GenerateAsync([text1]);
        var result2 = await generator.GenerateAsync([text2]);
        
        var embedding1 = result1[0].Vector.ToArray();
        var embedding2 = result2[0].Vector.ToArray();
        
        return CosineSimilarity(embedding1, embedding2);
    }

    /// <summary>
    /// Calcula similitud coseno entre dos vectores
    /// Retorna: 1.0 = idénticos, 0.0 = ortogonales, -1.0 = opuestos
    /// </summary>
    private double CosineSimilarity(float[] vector1, float[] vector2)
    {
        if (vector1.Length != vector2.Length)
            throw new ArgumentException("Los vectores deben tener la misma longitud");

        double dotProduct = 0;
        double magnitude1 = 0;
        double magnitude2 = 0;

        for (int i = 0; i < vector1.Length; i++)
        {
            dotProduct += vector1[i] * vector2[i];
            magnitude1 += vector1[i] * vector1[i];
            magnitude2 += vector2[i] * vector2[i];
        }

        magnitude1 = Math.Sqrt(magnitude1);
        magnitude2 = Math.Sqrt(magnitude2);

        if (magnitude1 == 0 || magnitude2 == 0)
            return 0;

        return dotProduct / (magnitude1 * magnitude2);
    }

    /// <summary>
    /// Convierte similitud coseno a distancia coseno (como la usa S3 Vectors)
    /// </summary>
    public double SimilarityToDistance(double similarity)
    {
        // Distancia coseno = 1 - similitud coseno
        // Rango: 0 (idénticos) a 2 (opuestos)
        return 1 - similarity;
    }

    /// <summary>
    /// Test de embeddings: genera embeddings para varios textos y muestra similitudes
    /// </summary>
    public async Task<List<EmbeddingTestResult>> RunEmbeddingTestAsync()
    {
        var results = new List<EmbeddingTestResult>();
        
        var generator = _kernel.Services.GetRequiredKeyedService<IEmbeddingGenerator<string, Embedding<float>>>("image-embeddings");

        var testCases = new[]
        {
            ("Camisa de vestir azul", "Camisa azul formal"),
            ("Pantalón de mezclilla", "Jeans azules"),
            ("Zapatos deportivos", "Tenis para correr"),
            ("Reloj de lujo", "Reloj elegante"),
            ("Bolsa de mano", "Cartera de cuero"),
            ("Gorra deportiva", "Sombrero casual")
        };

        foreach (var (text1, text2) in testCases)
        {
            var embedding1 = await generator.GenerateAsync([text1]);
            var embedding2 = await generator.GenerateAsync([text2]);

            var similarity = CosineSimilarity(embedding1[0].Vector.ToArray(), embedding2[0].Vector.ToArray());
            var distance = SimilarityToDistance(similarity);

            results.Add(new EmbeddingTestResult
            {
                Text1 = text1,
                Text2 = text2,
                Similarity = similarity,
                Distance = distance
            });
        }

        return results;
    }
}
