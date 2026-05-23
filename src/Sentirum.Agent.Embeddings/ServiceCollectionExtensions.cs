using Microsoft.Extensions.DependencyInjection;

namespace Sentirum.Agent.Embeddings;

/// <summary>
/// DI registration helpers for embeddings and vector stores.
/// </summary>
public static class SentirumEmbeddingsServiceCollectionExtensions
{
    /// <summary>
    /// Registers a named <see cref="InMemoryVectorStore{TKey}"/> as
    /// <see cref="IVectorStore{TKey}"/>.
    /// </summary>
    public static IServiceCollection AddInMemoryVectorStore<TKey>(
        this IServiceCollection services,
        string collectionName,
        int dimensions)
        where TKey : notnull
    {
        services.AddSingleton<IVectorStore<TKey>>(_ =>
            new InMemoryVectorStore<TKey>(collectionName, dimensions));

        return services;
    }

    /// <summary>
    /// Registers an <see cref="IEmbeddingGenerator"/> implementation
    /// using the supplied factory.
    /// </summary>
    public static IServiceCollection AddEmbeddingGenerator(
        this IServiceCollection services,
        Func<IServiceProvider, IEmbeddingGenerator> factory)
    {
        services.AddSingleton(factory);
        return services;
    }
}
