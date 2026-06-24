using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sentirum.Agent.CustomerSupport.Sentiment;

/// <summary>
/// A dependency-free, lexicon-based sentiment analyzer that recognises a
/// curated set of positive and negative terms in English and Turkish.
/// </summary>
/// <remarks>
/// <para>
/// This is intentionally a simple, transparent heuristic — it has no ML
/// dependency, is fully deterministic, and is trivially unit-testable. It
/// is a good default for triage and escalation gating; swap it for an
/// <c>ISentimentAnalyzer</c> backed by an embedding or chat model (wrapped
/// in <see cref="CachingSentimentAnalyzer"/>!) when you need higher
/// fidelity.
/// </para>
/// <para>
/// <b>Polarity</b> is the mean of the matched term weights, in [-1, 1].
/// </para>
/// <para>
/// <b>Negation.</b> English negators (<c>not</c>, <c>don't</c>, …) flip
/// the polarity of the next sentiment-bearing term within a small window
/// (so "not very happy" is negative). Turkish postfix negation
/// (<c>iyi değil</c>) and agglutinative morphology are intentionally not
/// modelled — Turkish complaints are dominated by strong negative terms
/// (<c>berbat</c>, <c>bozuk</c>, <c>kayıp</c>) that do not need negation.
/// </para>
/// </remarks>
public sealed class KeywordSentimentAnalyzer : ISentimentAnalyzer
{
    /// <summary>
    /// Maximum number of tokens a negator may sit ahead of the term it
    /// flips. Three covers intensifiers ("not very/really/so happy").
    /// </summary>
    private const int NegationWindow = 3;

    private static readonly Dictionary<string, double> Lexicon = BuildLexicon();

    /// <summary>
    /// Forward negators — flip the polarity of the next sentiment term
    /// within <see cref="NegationWindow"/>. English only (see remarks).
    /// </summary>
    private static readonly HashSet<string> Negators = new(StringComparer.Ordinal)
    {
        "not", "no", "don't", "doesn't", "didn't", "isn't", "wasn't",
        "aren't", "weren't", "hardly", "barely", "without", "neither", "nor",
    };

    /// <summary>
    /// A thread-safe singleton with the default lexicon.
    /// </summary>
    public static KeywordSentimentAnalyzer Instance { get; } = new();

    /// <inheritdoc />
    public ValueTask<SentimentScore> AnalyzeAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new ValueTask<SentimentScore>(new SentimentScore(0.0, SentimentLabel.Neutral, 1.0));
        }

        var normalized = Normalize(text);
        double sum = 0.0;
        var hits = 0;
        var negationDistance = -1; // -1 == no active negation

        // Single-pass tokenizer: scan the normalised string in place and
        // look each token up directly, avoiding an intermediate List.
        var i = 0;
        while (i < normalized.Length)
        {
            while (i < normalized.Length && !IsWordChar(normalized[i]))
            {
                i++;
            }

            var start = i;
            while (i < normalized.Length && IsWordChar(normalized[i]))
            {
                i++;
            }

            if (i == start)
            {
                break;
            }

            var token = normalized.Substring(start, i - start);

            if (Negators.Contains(token))
            {
                negationDistance = 0;
                continue;
            }

            if (Lexicon.TryGetValue(token, out var weight))
            {
                var negate = negationDistance is >= 0 and <= NegationWindow;
                sum += negate ? -weight : weight;
                hits++;
                negationDistance = -1; // consumed
            }
            else if (negationDistance >= 0)
            {
                negationDistance++;
                if (negationDistance > NegationWindow)
                {
                    negationDistance = -1;
                }
            }
        }

        if (hits == 0)
        {
            return new ValueTask<SentimentScore>(new SentimentScore(0.0, SentimentLabel.Neutral, 0.3));
        }

        var polarity = sum / hits;
        var label = polarity switch
        {
            > 0.15 => SentimentLabel.Positive,
            < -0.15 => SentimentLabel.Negative,
            _ => SentimentLabel.Neutral,
        };
        var confidence = Math.Min(1.0, 0.4 + (hits * 0.15) + (Math.Abs(polarity) * 0.2));

        return new ValueTask<SentimentScore>(new SentimentScore(polarity, label, confidence));
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '\'';

    /// <summary>
    /// Lower-cases the text and folds the Turkish dotless-i (ı, U+0131)
    /// onto dotted-i so that casing and the dotted/dotless-i ambiguity no
    /// longer affect matching. Applied to both lexicon keys and input, so
    /// <c>"KAYIP"</c>, <c>"kayıp"</c>, and <c>"kayip"</c> all canonicalise
    /// to the same key without fragile per-culture detection.
    /// </summary>
    private static string Normalize(string text)
    {
        // Invariant lowercasing maps I→i and İ→i. The dotless-i (ı) is
        // already lowercase, so it survives and must be folded explicitly.
        var lower = text.ToLowerInvariant();
        return lower.Contains('ı') ? lower.Replace('ı', 'i') : lower;
    }

    private static Dictionary<string, double> BuildLexicon()
    {
        // Weights are tuned for support-triage escalation: strong anger
        // signals score deep into the negative so they clear the default
        // escalation threshold even in short messages. Domain signal words
        // ("never", "asla") are stored as negative terms rather than
        // negators, because in a complaints context they almost always
        // indicate a problem ("never received", "asla gelmedi").
        var entries = new (string Word, double Weight)[]
        {
            // ── Negative ───────────────────────────────────────────────
            ("angry", -0.9), ("furious", -1.0), ("terrible", -1.0), ("horrible", -1.0),
            ("awful", -0.9), ("worst", -0.9), ("unacceptable", -0.9), ("disgusted", -0.9),
            ("frustrated", -0.8), ("annoyed", -0.7), ("disappointed", -0.8), ("broken", -0.7),
            ("damaged", -0.7), ("crushed", -0.8), ("missing", -0.5), ("stolen", -0.8),
            ("scam", -0.9), ("fraud", -0.9), ("refund", -0.3), ("complain", -0.6),
            ("rude", -0.8), ("useless", -0.8), ("garbage", -0.9),
            ("never", -0.5), ("cancel", -0.5), ("cancelled", -0.5), ("canceled", -0.5),
            ("lawsuit", -1.0), ("lawyer", -0.8), ("sue", -0.9), ("chargeback", -0.7),

            // Turkish negative
            ("kızgın", -0.9), ("sinirli", -0.8), ("berbat", -1.0), ("rezil", -0.9),
            ("iğrenç", -0.9), ("kötü", -0.6), ("bozuk", -0.7), ("ezik", -0.6),
            ("ezilmiş", -0.7), ("kayıp", -0.5), ("çalındı", -0.8), ("dolandırıcılık", -1.0),
            ("iade", -0.3), ("şikayet", -0.6), ("kaba", -0.8), ("asla", -0.5),
            ("iptal", -0.5), ("dava", -0.9), ("avukat", -0.8),

            // ── Positive ───────────────────────────────────────────────
            ("great", 0.8), ("excellent", 0.9), ("amazing", 0.9), ("wonderful", 0.9),
            ("happy", 0.8), ("satisfied", 0.8), ("thanks", 0.6), ("thank", 0.6),
            ("perfect", 0.9), ("love", 0.8), ("good", 0.6), ("awesome", 0.9),

            // Turkish positive
            ("harika", 0.9), ("mükemmel", 0.9), ("memnun", 0.8), ("teşekkür", 0.6),
            ("süper", 0.8), ("iyi", 0.5), ("güzel", 0.6),
        };

        var dict = new Dictionary<string, double>(entries.Length, StringComparer.Ordinal);
        foreach (var (word, weight) in entries)
        {
            dict[Normalize(word)] = weight;
        }
        return dict;
    }
}
