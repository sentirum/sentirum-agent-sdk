namespace Sentirum.Agent.CustomerSupport.Sentiment;

/// <summary>
/// Coarse-grained sentiment category produced by an
/// <see cref="ISentimentAnalyzer"/>.
/// </summary>
public enum SentimentLabel
{
    /// <summary>
    /// The text reads as positive or satisfied.
    /// </summary>
    Positive,

    /// <summary>
    /// The text is emotionally flat or mixed.
    /// </summary>
    Neutral,

    /// <summary>
    /// The text reads as negative, frustrated, or angry.
    /// </summary>
    Negative,
}
