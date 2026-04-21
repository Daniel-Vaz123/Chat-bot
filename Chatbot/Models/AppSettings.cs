namespace Chatbot.Models;

public class AppSettings
{
    public AwsSettings AWS { get; set; } = new();
    public S3VectorsSettings S3Vectors { get; set; } = new();
    public BedrockSettings Bedrock { get; set; } = new();
    public DeepSeekSettings DeepSeek { get; set; } = new();
}

public class AwsSettings
{
    public string Region { get; set; } = "us-east-1";
    public string? Profile { get; set; }
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
}

public class S3VectorsSettings
{
    public string BucketName { get; set; } = string.Empty;
    public string IndexName { get; set; } = string.Empty;
    public string ImageIndexName { get; set; } = string.Empty;
    public int TextEmbeddingDimensions { get; set; } = 1024;
    public int ImageEmbeddingDimensions { get; set; } = 1024;

    /// <summary>
    /// Rechaza el mejor match de S3 Vectors si la distancia es mayor que este valor (menor = más estricto).
    /// </summary>
    public double MaxQueryDistance { get; set; } = 0.75;
}

public class BedrockSettings
{
    public string TextEmbeddingModel { get; set; } = string.Empty;
    public string ImageEmbeddingModel { get; set; } = string.Empty;
}

public class DeepSeekSettings
{
    public string ApiKey { get; set; } = string.Empty;
}
