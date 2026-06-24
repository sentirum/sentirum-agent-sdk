using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sentirum.Agent.CustomerSupport.Sentiment;

/// <summary>
/// Decorates an <see cref="ISentimentAnalyzer"/> with a bounded LRU cache so
/// repeated messages (e.g. the same customer complaint re-scored by the
/// handler and the escalation provider) are analysed once.
/// </summary>
/// <remarks>
/// <para>
/// Use this to wrap an expensive analyzer (LLM-backed) so the per-request
/// escalation provider does not pay its cost on every turn. The default
/// <see cref="KeywordSentimentAnalyzer"/> is cheap enough that wrapping it is
/// optional.
/// </para>
/// <para>
/// The cache is an <see cref="int"/>-bounded least-recently-used map guarded
/// by a lock. Sentiment scoring is not a hot path, so a lock is simpler and
/// correct; the inner analyzer is never awaited under the lock.
/// </para>
/// </remarks>
public sealed class CachingSentimentAnalyzer : ISentimentAnalyzer
{
    private readonly ISentimentAnalyzer _inner;
    private readonly int _capacity;
    private readonly object _lock = new();
    private readonly LinkedList<CacheEntry> _order = new(); // MRU at the front
    private readonly Dictionary<string, LinkedListNode<CacheEntry>> _index;

    /// <summary>
    /// Initializes a new instance of the <see cref="CachingSentimentAnalyzer"/> class.
    /// </summary>
    /// <param name="inner">The analyzer to cache.</param>
    /// <param name="capacity">Maximum number of distinct messages to retain.</param>
    public CachingSentimentAnalyzer(ISentimentAnalyzer inner, int capacity = 256)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);

        _inner = inner;
        _capacity = capacity;
        _index = new Dictionary<string, LinkedListNode<CacheEntry>>(capacity, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public async ValueTask<SentimentScore> AnalyzeAsync(string text, CancellationToken cancellationToken = default)
    {
        var key = text ?? string.Empty;

        // Fast path: serve from cache. Returning inside the lock is fine —
        // we never await under it.
        lock (_lock)
        {
            if (_index.TryGetValue(key, out var node))
            {
                _order.Remove(node);
                _order.AddFirst(node); // promote to most-recently-used
                return node.Value.Score;
            }
        }

        var score = await _inner.AnalyzeAsync(key, cancellationToken).ConfigureAwait(false);

        // Slow path: insert. A concurrent caller may have computed the same
        // key in the meantime; refresh its recency instead of overwriting.
        lock (_lock)
        {
            if (_index.TryGetValue(key, out var existing))
            {
                _order.Remove(existing);
                _order.AddFirst(existing);
                return score;
            }

            var node = _order.AddFirst(new CacheEntry(key, score));
            _index[key] = node;

            while (_order.Count > _capacity)
            {
                var lru = _order.Last!;
                _order.RemoveLast();
                _index.Remove(lru.Value.Key);
            }
        }

        return score;
    }

    private readonly record struct CacheEntry(string Key, SentimentScore Score);
}
