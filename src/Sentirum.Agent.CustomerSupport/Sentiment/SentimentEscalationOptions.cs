using System;

namespace Sentirum.Agent.CustomerSupport.Sentiment;

/// <summary>
/// Options for <see cref="SentimentEscalationContextProvider"/> and the
/// <c>WithSentimentBasedEscalation</c> builder extension.
/// </summary>
public sealed class SentimentEscalationOptions
{
    /// <summary>
    /// Polarity at or below which the escalation instruction is injected.
    /// Defaults to <c>-0.3</c> (clearly negative).
    /// </summary>
    public double NegativeThreshold { get; set; } = -0.3;

    /// <summary>
    /// The minimum <see cref="SentimentScore.Confidence"/> required before
    /// the escalation fires. Guards against acting on weak signals.
    /// Defaults to <c>0.5</c>.
    /// </summary>
    public double MinimumConfidence { get; set; } = 0.5;

    /// <summary>
    /// The system instruction injected when a customer message clears the
    /// escalation threshold. Use it to steer tone, empathy, and the
    /// hand-off cue.
    /// </summary>
    public string EscalationInstructions { get; set; } =
        """
        ESCALATION TRIGGERED: The customer's message registers as strongly
        negative or frustrated. Lead with empathy, acknowledge the
        inconvenience, de-escalate the tone, and offer a concrete remedy.
        If the issue is beyond your tier, hand off to a specialist.
        """;

    /// <summary>
    /// Optional callback invoked for every analyzed message — positive,
    /// neutral, or negative. Use it to log sentiment or emit a metric.
    /// </summary>
    /// <remarks>
    /// Invoked concurrently across in-flight requests (the provider is a
    /// per-agent singleton), so it must be non-blocking and thread-safe.
    /// For per-request state, score the message directly at the call site
    /// rather than relying on this hook.
    /// </remarks>
    public Action<SentimentScore>? OnAnalyzed { get; set; }

    /// <summary>
    /// The analyzer to use. When <see langword="null"/>, the context
    /// provider resolves <see cref="ISentimentAnalyzer"/> from DI, falling
    /// back to <see cref="KeywordSentimentAnalyzer.Instance"/>.
    /// </summary>
    public ISentimentAnalyzer? Analyzer { get; set; }

    /// <summary>
    /// Validates the option values and returns this instance.
    /// </summary>
    internal SentimentEscalationOptions Validate()
    {
        if (MinimumConfidence is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MinimumConfidence), MinimumConfidence,
                "MinimumConfidence must be in [0, 1].");
        }
        return this;
    }
}
