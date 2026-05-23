using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sentirum.Agent.Context;

/// <summary>
/// Minimal retrieval contract used by the Sentirum knowledge-base context
/// provider. Implementations return ranked snippets for a free-text query.
/// </summary>
/// <remarks>
/// Defining this as a thin interface (rather than depending on
/// <c>Microsoft.Extensions.VectorData</c> directly) keeps Sentirum
/// retrieval-backend-agnostic: callers can plug in an in-memory FAQ list,
/// a SQL FTS index, an embeddings vector store, or a remote search API.
/// </remarks>
public interface IKnowledgeBase
{
    /// <summary>
    /// Returns the top-<paramref name="maxResults"/> snippets matching
    /// <paramref name="query"/>.
    /// </summary>
    Task<IReadOnlyList<KnowledgeBaseSnippet>> SearchAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A single ranked snippet returned by an <see cref="IKnowledgeBase"/>.
/// </summary>
/// <param name="Title">Short title or document name.</param>
/// <param name="Content">Body text to inject into the prompt.</param>
/// <param name="Score">Relevance score (provider-defined scale).</param>
/// <param name="SourceUrl">Optional URL pointing back at the source.</param>
public readonly record struct KnowledgeBaseSnippet(
    string Title,
    string Content,
    double Score,
    string? SourceUrl = null);
