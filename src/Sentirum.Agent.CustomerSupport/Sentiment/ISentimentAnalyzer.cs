using System.Threading;
using System.Threading.Tasks;

namespace Sentirum.Agent.CustomerSupport.Sentiment;

/// <summary>
/// Analyzes a piece of customer text and returns a
/// <see cref="SentimentScore"/>.
/// </summary>
/// <remarks>
/// The default implementation is the dependency-free
/// <see cref="KeywordSentimentAnalyzer"/>. Plug in an LLM-backed analyzer
/// (or any other scorer) by registering it in DI; the
/// <see cref="SentimentEscalationContextProvider"/> resolves it per request.
/// </remarks>
public interface ISentimentAnalyzer
{
    /// <summary>
    /// Scores the supplied <paramref name="text"/>.
    /// </summary>
    /// <param name="text">The customer message to analyze.</param>
    /// <param name="cancellationToken">
    /// Token to cancel the analysis.</param>
    /// <returns>A <see cref="SentimentScore"/>.</returns>
    ValueTask<SentimentScore> AnalyzeAsync(string text, CancellationToken cancellationToken = default);
}
