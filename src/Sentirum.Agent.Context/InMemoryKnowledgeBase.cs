using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Sentirum.Agent.Context;

/// <summary>
/// Trivial in-process <see cref="IKnowledgeBase"/> backed by a list of
/// snippets and a case-insensitive substring score. Intended for samples,
/// tests, and seed data; real deployments should plug in a vector store
/// or external search.
/// </summary>
public sealed class InMemoryKnowledgeBase : IKnowledgeBase
{
    private static readonly char[] WordSeparators =
        { ' ', '.', ',', ';', ':', '!', '?', '\n', '\r', '\t', '(', ')', '"', '\'' };

    private readonly IReadOnlyList<KnowledgeBaseSnippet> _snippets;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryKnowledgeBase"/> class.
    /// </summary>
    public InMemoryKnowledgeBase(IEnumerable<KnowledgeBaseSnippet> snippets)
    {
        ArgumentNullException.ThrowIfNull(snippets);
        _snippets = snippets.ToList();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<KnowledgeBaseSnippet>> SearchAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxResults, 1);
        cancellationToken.ThrowIfCancellationRequested();

        // Naive scoring: count case-insensitive token matches in title +
        // content. Good enough for FAQ samples; do not ship this against
        // real KBs.
        var tokens = query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 1)
            .Select(t => t.ToLowerInvariant())
            .ToArray();

        IReadOnlyList<KnowledgeBaseSnippet> ranked = _snippets
            .Select(s => new
            {
                Snippet = s,
                Score = ScoreOne(s, tokens),
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(maxResults)
            .Select(x => x.Snippet with { Score = x.Score })
            .ToList();

        return Task.FromResult(ranked);
    }

    private static double ScoreOne(KnowledgeBaseSnippet s, string[] tokens)
    {
        if (tokens.Length == 0)
        {
            return 0;
        }

        // Tokenize the haystack the same way as the query so matches are
        // word-boundary, not substring. Cheap but it stops false positives
        // like "me" inside "customers" from inflating scores.
        var haystackTokens = new System.Collections.Generic.HashSet<string>(
            (s.Title + " " + s.Content)
                .ToLowerInvariant()
                .Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries),
            StringComparer.Ordinal);

        var hits = 0;
        foreach (var token in tokens)
        {
            if (haystackTokens.Contains(token))
            {
                hits++;
            }
        }

        return hits / (double)tokens.Length;
    }
}
