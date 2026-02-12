namespace CAF.Services.VectorDB;

/// <summary>
/// Factory for creating QdrantService instances with different collections
/// </summary>
public interface IQdrantServiceFactory
{
    /// <summary>
    /// Creates a QdrantService instance for the specified collection
    /// </summary>
    IQdrantService CreateService(string collectionName);
}

/// <summary>
/// Implementation of IQdrantServiceFactory
/// </summary>
public class QdrantServiceFactory(IOptions<QdrantOptions> options, ILogger<QdrantService> logger) : IQdrantServiceFactory
{
    public IQdrantService CreateService(string collectionName)
    {
        return new QdrantService(collectionName, options, logger);
    }
}