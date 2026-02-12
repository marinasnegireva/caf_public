namespace CAF.Services.VectorDB;

/// <summary>
/// Configuration options for Qdrant vector database
/// </summary>
public class QdrantOptions
{
    public const string SectionName = "Qdrant";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6334;
}