namespace Sentirum.Agent.CustomerSupport.Sentiment;

/// <summary>
/// The result of analyzing a single piece of text. Polarity ranges from
/// <c>-1.0</c> (strongly negative) through <c>0.0</c> (neutral) to
/// <c>+1.0</c> (strongly positive).
/// </summary>
public readonly record struct SentimentScore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SentimentScore"/> struct.
    /// </summary>
    public SentimentScore(double polarity, SentimentLabel label, double confidence = 1.0)
    {
        // Clamp polarity into [-1, 1] so downstream thresholds behave
        // predictably regardless of the analyzer implementation.
        Polarity = Math.Max(-1.0, Math.Min(1.0, polarity));
        Label = label;
        Confidence = Math.Max(0.0, Math.Min(1.0, confidence));
    }

    /// <summary>
    /// The signed sentiment strength in the range [-1, 1].
    /// </summary>
    public double Polarity { get; }

    /// <summary>
    /// The discrete bucket the polarity falls into.
    /// </summary>
    public SentimentLabel Label { get; }

    /// <summary>
    /// Analyzer-specific confidence in the range [0, 1].
    /// </summary>
    public double Confidence { get; }

    /// <summary>
    /// <see langword="true"/> when the score indicates negative sentiment.
    /// </summary>
    public bool IsNegative => Label == SentimentLabel.Negative || Polarity < 0.0;
}
