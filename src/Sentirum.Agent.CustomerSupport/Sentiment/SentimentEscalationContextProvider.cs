using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Sentirum.Agent.Context;

namespace Sentirum.Agent.CustomerSupport.Sentiment;

/// <summary>
/// <see cref="MessageAIContextProvider"/> that scores the latest customer
/// message for negative sentiment and, when it clears a configurable
/// threshold, injects an escalation instruction into the request.
/// </summary>
/// <remarks>
/// <para>
/// This is the "sentiment + escalation triggers as a context provider"
/// building block called out in the planning document. It keeps the
/// sentiment decision out of the agent's own prompt — the model simply
/// receives an extra system message that steers its tone and cues a
/// hand-off when the customer is frustrated.
/// </para>
/// <para>
/// We derive from <see cref="MessageAIContextProvider"/> (not the more
/// general <see cref="AIContextProvider"/>) so
/// <see cref="MessageAIContextProvider.InvokingContext.RequestMessages"/>
/// surfaces the just-arrived user turn — the same reason
/// <c>KnowledgeBaseContextProvider</c> does.
/// </para>
/// <para>
/// <b>Concurrency.</b> The provider is a per-agent singleton, so the
/// <see cref="SentimentEscalationOptions.OnAnalyzed"/> callback is invoked
/// concurrently across in-flight requests. It must be non-blocking and
/// thread-safe; treat it like an event handler (log/emit a metric), and
/// do not mutate per-request state in it — score the message directly
/// at the call site for that.
/// </para>
/// </remarks>
public sealed class SentimentEscalationContextProvider : MessageAIContextProvider
{
    private readonly ISentimentAnalyzer _analyzer;
    private readonly double _negativeThreshold;
    private readonly double _minimumConfidence;
    private readonly string _escalationInstructions;
    private readonly Action<SentimentScore>? _onAnalyzed;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="SentimentEscalationContextProvider"/> class.
    /// </summary>
    /// <param name="analyzer">The sentiment analyzer to run on each turn.</param>
    /// <param name="options">Configured escalation options.</param>
    /// <remarks>
    /// The analyzer is taken explicitly so the builder extension can resolve
    /// a DI-registered <see cref="ISentimentAnalyzer"/> via the deferred
    /// options pipeline (see ADR-0005), matching how
    /// <c>WithKnowledgeBase&lt;T&gt;()</c> resolves its knowledge base.
    /// </remarks>
    public SentimentEscalationContextProvider(ISentimentAnalyzer analyzer, SentimentEscalationOptions options)
    {
        ArgumentNullException.ThrowIfNull(analyzer);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        _analyzer = analyzer;
        _negativeThreshold = options.NegativeThreshold;
        _minimumConfidence = options.MinimumConfidence;
        _escalationInstructions = options.EscalationInstructions;
        _onAnalyzed = options.OnAnalyzed;
    }

    /// <summary>
    /// The threshold used to decide escalation. Exposed for diagnostics.
    /// </summary>
    public double NegativeThreshold => _negativeThreshold;

    /// <inheritdoc />
    protected override async ValueTask<IEnumerable<ChatMessage>> ProvideMessagesAsync(
        MessageAIContextProvider.InvokingContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var text = ChatMessageQueries.LatestUserText(context.RequestMessages);
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<ChatMessage>();
        }

        var score = await _analyzer.AnalyzeAsync(text, cancellationToken).ConfigureAwait(false);
        _onAnalyzed?.Invoke(score);

        // Only escalate on confident negative signals — a weakly negative
        // low-confidence score should not trigger a hand-off.
        if (score.Polarity <= _negativeThreshold && score.Confidence >= _minimumConfidence)
        {
            return new[]
            {
                new ChatMessage(ChatRole.System, _escalationInstructions.Trim()),
            };
        }

        return Array.Empty<ChatMessage>();
    }
}
